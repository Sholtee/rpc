﻿/********************************************************************************
* RoleValidatorTests.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Security.Authentication;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
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
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            var mockRoleManager = new Mock<IRoleManager>(MockBehavior.Strict);
            mockRoleManager
                .Setup(rm => rm.GetAssignedRoles("cica"))
                .Returns(data.Roles);

            var mockRequest = new Mock<IRequestContext>(MockBehavior.Strict);
            mockRequest
                .SetupGet(r => r.SessionId)
                .Returns("cica");

            Type proxyType = ProxyGenerator<IModule, RoleValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockRequest.Object, mockRoleManager.Object);

            if (data.ShouldThrow)
                Assert.Throws<AuthenticationException>(module.Print, Errors.INSUFFICIENT_PRIVILEGES);
            else
                Assert.DoesNotThrow(module.Print);

            mockRequest.VerifyGet(r => r.SessionId, Times.Once);
            mockRoleManager.Verify(rm => rm.GetAssignedRoles("cica"), Times.Once);
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

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockRequest.Object, mockRoleManager.Object);

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

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockRequest.Object, mockRoleManager.Object);

            Assert.Throws<InvalidOperationException>(module.MissingRequiredRoleAttribute, Errors.NO_ROLES_SPECIFIED);
        }
    }
}
