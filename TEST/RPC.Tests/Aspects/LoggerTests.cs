/********************************************************************************
* LoggerTests.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Primitives.Patterns;
    using Proxy.Generators;

    [TestFixture]
    public class LoggerTests
    {
        [LoggerAspect]
        public interface IModule
        {
            void DoSomething(string arg1, object arg2);

            [Loggers(typeof(ModuleMethodScopeLogger))]
            void DoSomethingElse();
        }

        public Mock<ILogger> Logger { get; set; }

        public Mock<IInjector> Injector { get; set; }

        public Mock<IModule> Module { get; set; }

        [SetUp]
        public void SetupTest()
        {
            Logger = new Mock<ILogger>(MockBehavior.Loose);
            Injector = new Mock<IInjector>(MockBehavior.Strict);
            Module = new Mock<IModule>(MockBehavior.Loose);
        }

        [Test]
        public void DefaultLoggers_ShouldBeAppliedByDefault()
        {
            Injector
                .Setup(i => i.Get(typeof(IRequestContext), null))
                .Returns(new RequestContext("cica", nameof(IModule), nameof(IModule.DoSomething), null, default));

            int callOrder = 0;

            Logger
                .Setup(l => l.BeginScope(It.Is<Dictionary<string, object>>(d => d["Module"].ToString() == nameof(IModule) && d["Method"].ToString() == nameof(IModule.DoSomething) && d["SessionId"].ToString() == "cica")))
                .Returns<Dictionary<string, object>>(_ => 
                {
                    Assert.That(callOrder++, Is.EqualTo(0));
                    return new Disposable();
                });
            /*
            Logger
                .Setup(l => l.Log(LogLevel.Information, It.Is<string>(s => s.StartsWith("Time elapsed:"))))
                .Callback<string>(_ => Assert.That(callOrder++, Is.EqualTo(1)));
            */
            Type proxyType = ProxyGenerator<IModule, Logger<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, Module.Object, Injector.Object, Logger.Object);
            Assert.DoesNotThrow(() => module.DoSomething("cica", 1));
            Assert.That(callOrder, Is.EqualTo(1));
        }

        [Test]
        public void DefaultLoggers_CanBeOverridden()
        {
        }
    }
}
