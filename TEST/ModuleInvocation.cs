/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text.Json;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.AppHost.Tests
{
    using DI.Interfaces;
    using Internals;

    [TestFixture]
    public class ModuleInvocationTests
    {
        public interface IService
        {
            int Add(int a, int b);
            void Void();
            [Alias("Cica")]
            void Bar();
        }

        private static ModuleInvocation GetModuleInvocation() 
        {
            var bldr = new ModuleInvocationBuilder();
            bldr.AddModule<IService>();
            return bldr.Build();
        }

        private ModuleInvocation Invocation;

        [OneTimeSetUp]
        public void SetupFixture() => Invocation = GetModuleInvocation();

        [Test]
        public void ModuleInvocation_ShouldQueryTheService() 
        {
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(new Mock<IService>().Object);

            Invocation.Invoke(mockInjector.Object, nameof(IService), nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, 2 }));

            mockInjector.Verify(i => i.Get(It.IsAny<Type>(), null), Times.Once);
        }

        [Test]
        public void ModuleInvocation_ShouldCallTheDesiredMethod() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService
                .Setup(svc => svc.Add(1, 2))
                .Returns<int, int>((a, b) => a + b);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.That(Invocation.Invoke(mockInjector.Object, nameof(IService), nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, 2 })), Is.EqualTo(3));
            mockService.Verify(svc => svc.Add(1, 2), Times.Once);
        }

        [Test]
        public void ModuleInvocation_ShouldThrowIfTheModuleNotFound() 
        {
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Assert.Throws<ServiceNotFoundException>(() => Invocation.Invoke(mockInjector.Object, "Invalid", nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, 2 })));
        }

        [Test]
        public void ModuleInvocation_ShouldThrowIfTheMethodNotFound() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.Throws<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, nameof(IService), "Invalid", null));
        }

        [Test]
        public void ModuleInvocation_ShouldThrowOnInvalidParameter() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.Throws<JsonException>(() => Invocation.Invoke(mockInjector.Object, nameof(IService), nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, })));
        }

        [Test]
        public void ModuleInvocation_ShouldWorkWithVoidMethods() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService
                .Setup(svc => svc.Void());

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.IsNull(Invocation.Invoke(mockInjector.Object, nameof(IService), nameof(IService.Void), JsonSerializer.Serialize(new object[0])));
            mockService.Verify(svc => svc.Void(), Times.Once);
        }

        [Test]
        public void ModuleInvocation_ShouldHandleMethodAlias()
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService
                .Setup(svc => svc.Bar());

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.Throws<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, nameof(IService), nameof(IService.Bar), JsonSerializer.Serialize(new object[0])));
            Assert.DoesNotThrow(() => Invocation.Invoke(mockInjector.Object, nameof(IService), "Cica", JsonSerializer.Serialize(new object[0])));
        }
    }
}
