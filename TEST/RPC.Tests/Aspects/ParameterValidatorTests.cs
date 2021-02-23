/********************************************************************************
* ParameterValidatorTests.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
    using DI;
    using DI.Interfaces;
    using Interfaces;
    using Proxy.Generators;

    [TestFixture]
    public class ParameterValidatorTests
    {
        [ParameterValidatorAspect]
        public interface IModule
        {
            void DoSomething([NotNull] string arg1, [NotNull] object arg2);
            void DoSomethingElse();
        }

        [Test]
        public void ParameterValidationTest()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, false)!;

            Assert.DoesNotThrow(() => module.DoSomething("cica", 1));
            Assert.Throws<ValidationException>(() => module.DoSomething(null, null));
        }

        [Test]
        public void AggregatedParameterValidationTest()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, true)!;

            Assert.DoesNotThrow(() => module.DoSomething("cica", 1));
            AggregateException ex = Assert.Throws<AggregateException>(() => module.DoSomething(null, null));
            Assert.That(ex.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(ex.InnerExceptions[0], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[0]).Name, Is.EqualTo("arg1"));
            Assert.That(ex.InnerExceptions[1], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[1]).Name, Is.EqualTo("arg2"));
        }

        [Test]
        public void ParameterValidatorAspect_ShouldSpecializeTheParameterValidator()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);

            using (IServiceContainer container = new ServiceContainer())
            {
                container.Factory(injector => mockModule.Object, Lifetime.Scoped);

                IInjector injector = container.CreateInjector();
                IModule module = injector.Get<IModule>();

                Assert.That(module, Is.InstanceOf<ParameterValidator<IModule>>());
            }
        }
    }
}
