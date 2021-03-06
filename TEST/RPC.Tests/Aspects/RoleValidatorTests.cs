﻿/********************************************************************************
* RoleValidatorTests.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
    using DI;
    using DI.Interfaces;
    using Interfaces;
    using Properties;
    using Proxy.Generators;

    [TestFixture]
    public class RoleValidatorTests
    {
        [Flags]
        public enum MyRoles
        {
            Anonymous = 0,
            User = 1,
            MayPrint = 2,
            Admin = 4
        }

        [RoleValidatorAspect]
        public interface IModule
        {
            [RequiredRoles(MyRoles.User | MyRoles.MayPrint, MyRoles.Admin)]
            void Print();
            [RequiredRoles(MyRoles.User | MyRoles.MayPrint, MyRoles.Admin)]
            Task<string> PrintAsync();
            [RequiredRoles(MyRoles.Anonymous)]
            void Login();
            void MissingRequiredRoleAttribute();
        }

        public static IEnumerable<(MyRoles Roles, bool ShouldThrow)> TestCases 
        {
            get 
            {
                yield return (MyRoles.Anonymous, true);
                yield return (MyRoles.User, true);
                yield return (MyRoles.User | MyRoles.MayPrint, false);
                yield return (MyRoles.Admin, false);
            }
        }

        [TestCaseSource(nameof(TestCases))]
        public void RoleValidationTest((MyRoles Roles, bool ShouldThrow) data)
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.Print());

            var mockRoleManager = new Mock<IRoleManager>(MockBehavior.Strict);
            mockRoleManager
                .Setup(rm => rm.GetAssignedRoles("cica"))
                .Returns(data.Roles);

            var mockRequest = new Mock<IRequestContext>(MockBehavior.Strict);
            mockRequest
                .SetupGet(r => r.SessionId)
                .Returns("cica");

            Type proxyType = ProxyGenerator<IModule, RoleValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockRequest.Object, mockRoleManager.Object, null);

            if (data.ShouldThrow)
            {
                Assert.Throws<AuthenticationException>(module.Print, Errors.INSUFFICIENT_PRIVILEGES);
                mockModule.Verify(m => m.Print(), Times.Never);
            }
            else
                Assert.DoesNotThrow(module.Print);

            mockRequest.VerifyGet(r => r.SessionId, Times.Once);
            mockRoleManager.Verify(rm => rm.GetAssignedRoles("cica"), Times.Once);
        }

        [TestCaseSource(nameof(TestCases))]
        public void RoleValidationAsyncTest((MyRoles Roles, bool ShouldThrow) data)
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.PrintAsync())
                .Returns(Task.FromResult("kutya"));

            var mockRoleManager = new Mock<IAsyncRoleManager>(MockBehavior.Strict);
            mockRoleManager
                .Setup(rm => rm.GetAssignedRolesAsync("cica", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((Enum) data.Roles));

            var mockRequest = new Mock<IRequestContext>(MockBehavior.Strict);
            mockRequest
                .SetupGet(r => r.SessionId)
                .Returns("cica");
            mockRequest
                .SetupGet(r => r.Cancellation)
                .Returns(default(CancellationToken));

            Type proxyType = ProxyGenerator<IModule, RoleValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockRequest.Object, new Mock<IRoleManager>(MockBehavior.Strict).Object, mockRoleManager.Object);

            if (data.ShouldThrow)
            {
                Assert.ThrowsAsync<AuthenticationException>(module.PrintAsync, Errors.INSUFFICIENT_PRIVILEGES);
                mockModule.Verify(m => m.PrintAsync(), Times.Never);
            }
            else
            {
                string result = null;
                Assert.DoesNotThrowAsync(async () => result = await module.PrintAsync());
                Assert.That(result, Is.EqualTo("kutya"));
            }

            mockRequest.VerifyGet(r => r.SessionId, Times.Once);
            mockRequest.VerifyGet(r => r.Cancellation, Times.Once);
            mockRoleManager.Verify(rm => rm.GetAssignedRolesAsync("cica", default), Times.Once);
        }

        [Test]
        public void RoleValidator_ShouldAllowAnonymAccess([Values(MyRoles.Admin, MyRoles.Anonymous)] MyRoles roles)
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            string sessionId = roles == MyRoles.Anonymous ? null : "cica";

            var mockRoleManager = new Mock<IRoleManager>(MockBehavior.Strict);
            mockRoleManager
                .Setup(rm => rm.GetAssignedRoles(sessionId))
                .Returns(roles);

            var mockRequest = new Mock<IRequestContext>(MockBehavior.Strict);
            mockRequest
                .SetupGet(r => r.SessionId)
                .Returns(sessionId);

            Type proxyType = ProxyGenerator<IModule, RoleValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockRequest.Object, mockRoleManager.Object, null);

            Assert.DoesNotThrow(module.Login);

            mockRequest.VerifyGet(r => r.SessionId, Times.Once);
            mockRoleManager.Verify(rm => rm.GetAssignedRoles(sessionId), Times.Once);
        }

        [Test]
        public void RoleValidator_ShouldThrowOnMissingRequiredRolesAttribute() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            var mockRoleManager = new Mock<IRoleManager>(MockBehavior.Strict);;

            var mockRequest = new Mock<IRequestContext>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IModule, RoleValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockRequest.Object, mockRoleManager.Object, null);

            Assert.Throws<InvalidOperationException>(module.MissingRequiredRoleAttribute, Errors.NO_ROLES_SPECIFIED);
        }

        [Test]
        public void RequiredRoleAttribute_ShouldThrowOnNull() => Assert.Throws<ArgumentNullException>(() => new RequiredRolesAttribute(null));

        [Test]
        public void RequiredRoleAttribute_ShouldThrowIfThereIsNoRoleSpecified() => Assert.Throws<ArgumentException>(() => new RequiredRolesAttribute(), Errors.NO_ROLES_SPECIFIED);

        [Test]
        public void RoleValidatorAspect_ShouldSpecializeTheRoleValidator()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);

            var mockRoleManager = new Mock<IRoleManager>(MockBehavior.Strict);

            var mockRequest = new Mock<IRequestContext>(MockBehavior.Strict);

            using (IServiceContainer container = new ServiceContainer())
            {
                container
                    .Factory(injector => mockModule.Object, Lifetime.Scoped)
                    .Factory(injector => mockRoleManager.Object, Lifetime.Scoped);

                IInjector injector = container.CreateInjector();
                injector.UnderlyingContainer
                    .Instance(mockRequest.Object);

                IModule module = injector.Get<IModule>();

                Assert.That(module, Is.InstanceOf<RoleValidator<IModule>>());
            }
        }
    }
}
