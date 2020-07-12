/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text.Json;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
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
            [Ignore]
            void Ignored();
            Task Async();
            Task<int> AsyncHavingRetVal();
            [MayRunLong]
            void LongRunning();
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
        public async Task ModuleInvocation_ShouldQueryTheService() 
        {
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(new Mock<IService>().Object);

            await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, 2 }), null));

            mockInjector.Verify(i => i.Get(It.IsAny<Type>(), null), Times.Once);
        }

        [Test]
        public async Task ModuleInvocation_ShouldCallTheDesiredMethod() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService
                .Setup(svc => svc.Add(1, 2))
                .Returns<int, int>((a, b) => a + b);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.That(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, 2 }), null)), Is.EqualTo(3));
            mockService.Verify(svc => svc.Add(1, 2), Times.Once);
        }

        [Test]
        public void ModuleInvocation_ShouldThrowIfTheModuleNotFound() 
        {
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Assert.ThrowsAsync<ServiceNotFoundException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, "Invalid", nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, 2 }), null)));
        }

        [Test]
        public void ModuleInvocation_ShouldThrowIfTheMethodNotFound() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.ThrowsAsync<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), "Invalid", null, null)));
        }

        [Test]
        public void ModuleInvocation_ShouldThrowOnInvalidParameter() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.ThrowsAsync<JsonException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Add), JsonSerializer.Serialize(new object[] { 1, }), null)));
        }

        [Test]
        public async Task ModuleInvocation_ShouldWorkWithVoidMethods() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService
                .Setup(svc => svc.Void());

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.IsNull(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Void), JsonSerializer.Serialize(new object[0]), null)));
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

            Assert.ThrowsAsync<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Bar), JsonSerializer.Serialize(new object[0]), null)));
            Assert.DoesNotThrowAsync(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), "Cica", JsonSerializer.Serialize(new object[0]), null)));
        }

        [Test]
        public void ModuleInvocation_ShouldTakeIgnoreAttributeIntoAccount() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.ThrowsAsync<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Ignored), JsonSerializer.Serialize(new object[0]), null)));
        }

        [Test]
        public async Task ModuleInvocation_ShouldHandleTasks() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService
                .Setup(i => i.Async())
                .Returns(Task.CompletedTask);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.That(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Async), JsonSerializer.Serialize(new object[0]), null)), Is.Null);
            mockService.Verify(i => i.Async(), Times.Once);
        }

        [Test]
        public async Task ModuleInvocation_ShouldHandleTasksHavingRetVal()
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService
                .Setup(i => i.AsyncHavingRetVal())
                .Returns(Task.FromResult(1));

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.That(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.AsyncHavingRetVal), JsonSerializer.Serialize(new object[0]), null)), Is.EqualTo(1));
            mockService.Verify(i => i.AsyncHavingRetVal(), Times.Once);
        }

        [Test]
        public void ModuleInvocation_ShouldCreateALongRunningTaskIfNecessary() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);
            mockService.Setup(i => i.LongRunning());

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Task result = Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.LongRunning), JsonSerializer.Serialize(new object[0]), null));
            Assert.That(result.CreationOptions.HasFlag(TaskCreationOptions.LongRunning));
        }
    }
}
