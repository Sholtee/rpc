/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            void LongRunning();
        }

        private static ModuleInvocation GetModuleInvocation() 
        {
            var bldr = new ModuleInvocationBuilder();
            bldr.AddModule<IService>();
            return bldr.Build();
        }

        private ModuleInvocation Invocation;

        private static Stream AsStream(params object[] values) 
        {
            var result = new MemoryStream();

            var sw = new StreamWriter(result);
            sw.Write(JsonSerializer.Serialize(values));
            sw.Flush();

            result.Seek(0, SeekOrigin.Begin);

            return result;
        }

        [OneTimeSetUp]
        public void SetupFixture() => Invocation = GetModuleInvocation();

        [Test]
        public async Task ModuleInvocation_ShouldQueryTheService() 
        {
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(new Mock<IService>().Object);

            await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Add), AsStream(1, 2), default));

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

            Assert.That(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Add), AsStream(1, 2), default)), Is.EqualTo(3));
            mockService.Verify(svc => svc.Add(1, 2), Times.Once);
        }

        [Test]
        public void ModuleInvocation_ShouldThrowIfTheModuleNotFound() 
        {
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Assert.ThrowsAsync<ServiceNotFoundException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, "Invalid", nameof(IService.Add), AsStream(1, 2), default)));
        }

        [Test]
        public void ModuleInvocation_ShouldThrowIfTheMethodNotFound() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.ThrowsAsync<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), "Invalid", null, default)));
        }

        public static IEnumerable<Stream> InvalidPayloads 
        {
            get 
            {
                //
                // Kevesebb
                //

                yield return AsStream();
                yield return AsStream(1);

                //
                // Tobb
                //

                yield return AsStream(1, 2, 3);

                //
                // Nem megfelelo parameter
                //

                yield return AsStream(1, "2");
                yield return AsStream(new object(), new object());
            }
        }

        [TestCaseSource(nameof(InvalidPayloads))]
        public void ModuleInvocation_ShouldThrowOnInvalidParameter(Stream payload) 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.ThrowsAsync<JsonException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Add), payload, default)));
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

            Assert.IsNull(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Void), AsStream(), default)));
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

            Assert.ThrowsAsync<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Bar), AsStream(), default)));
            Assert.DoesNotThrowAsync(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), "Cica", AsStream(), default)));
        }

        [Test]
        public void ModuleInvocation_ShouldTakeIgnoreAttributeIntoAccount() 
        {
            var mockService = new Mock<IService>(MockBehavior.Strict);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IService), null))
                .Returns(mockService.Object);

            Assert.ThrowsAsync<MissingMethodException>(() => Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Ignored), AsStream(), default)));
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

            Assert.That(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.Async), AsStream(), default)), Is.Null);
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

            Assert.That(await Invocation.Invoke(mockInjector.Object, new RequestContext(null, nameof(IService), nameof(IService.AsyncHavingRetVal), AsStream(), default)), Is.EqualTo(1));
            mockService.Verify(i => i.AsyncHavingRetVal(), Times.Once);
        }

        public static IEnumerable<Type> RandomInterfaces => typeof(object)
            .Assembly
            .GetExportedTypes()
            .Where
            (
                t => t.IsInterface && !t.ContainsGenericParameters && !ModuleInvocationBuilder.GetAllInterfaceMethods(t).Any
                (
                    m => m.GetParameters().Any(p => p.ParameterType.IsByRef)
                )
            );

        [TestCaseSource(nameof(RandomInterfaces))]
        public void ModuleInvocationBuilder_ShouldWorkWith(Type iface) => Assert.DoesNotThrow(() =>
        {
            var bldr = new ModuleInvocationBuilder();
            bldr.AddModule(iface);
            bldr.Build();
        });

        [Test]
        public void ModuleInvocationBuilder_ShouldHandleMultipleModules() => Assert.DoesNotThrow(() =>
        {
            var bldr = new ModuleInvocationBuilder();
            foreach(Type iface in RandomInterfaces) bldr.AddModule(iface);
            bldr.Build();
        });

        [Test]
        public void ModuleInvocationBuilder_ShouldThrowOnByRefParameter() => Assert.Throws<ArgumentException>(() => 
        {
            var bldr = new ModuleInvocationBuilder();
            bldr.AddModule<IDictionary<string, string>>();
        });

        [Test]
        public void GetRelatedModules_ShouldDoWhatItsNameSuggests() => Assert.That(Invocation.GetRelatedModules().Single(), Is.EqualTo(typeof(IService)));
    }
}
