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
        [ModuleLoggerAspect]
        public interface IModule
        {
            void DoSomething(string arg1, object arg2);

            [Loggers(typeof(ModuleMethodScopeLogger))]
            void DoSomethingElse();
        }

        private class LogForwarder : ILogger
        {
            public Func<object, IDisposable> BeginScope { get; }

            public Action<LogLevel, EventId, string> Log { get; }

            public LogForwarder(Func<object, IDisposable> beginScope, Action<LogLevel, EventId, string> log)
            {
                BeginScope = beginScope;
                Log = log;
            }

            IDisposable ILogger.BeginScope<TState>(TState state) => BeginScope(state);

            bool ILogger.IsEnabled(LogLevel logLevel) => true;

            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) => Log(logLevel, eventId, formatter(state, exception));
        }

        public Mock<Func<object, IDisposable>> BeginScope { get; set; }

        public Mock<Action<LogLevel, EventId, string>> Log { get; set; }

        public Mock<IInjector> Injector { get; set; }

        public Mock<IModule> Module { get; set; }

        [SetUp]
        public void SetupTest()
        {
            BeginScope = new(MockBehavior.Strict);
            Log = new(MockBehavior.Strict);
            Injector = new (MockBehavior.Strict);
            Module = new (MockBehavior.Loose);
        }

        [Test]
        public void DefaultLoggers_ShouldBeAppliedByDefault()
        {
            Injector
                .Setup(i => i.Get(typeof(IRequestContext), null))
                .Returns(new RequestContext("cica", nameof(IModule), nameof(IModule.DoSomething), null, default));

            int callOrder = 0;

            BeginScope
                .Setup(fn => fn(It.Is<Dictionary<string, object>>(d => d["Module"].ToString() == nameof(IModule) && d["Method"].ToString() == nameof(IModule.DoSomething) && d["SessionId"].ToString() == "cica")))
                .Returns<Dictionary<string, object>>(_ => 
                {
                    Assert.That(callOrder++, Is.EqualTo(0));
                    return new Disposable();
                });
            Log
                .Setup(fn => fn(LogLevel.Information, default, $"Parameters: arg1:cica,{Environment.NewLine}arg2:1"))
                .Callback<LogLevel, EventId, string>((_, _, _) => Assert.That(callOrder++, Is.EqualTo(1)));
            Log
                .Setup(fn => fn(LogLevel.Information, default, It.Is<string>(s => s.StartsWith("Time elapsed:"))))
                .Callback<LogLevel, EventId, string>((_, _, _) => Assert.That(callOrder++, Is.EqualTo(2)));
            Type proxyType = ProxyGenerator<IModule, Logger<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, Module.Object, Injector.Object, new LogForwarder(BeginScope.Object, Log.Object));
            Assert.DoesNotThrow(() => module.DoSomething("cica", 1));
            Assert.That(callOrder, Is.EqualTo(3));
        }

        [Test]
        public void DefaultLoggers_CanBeOverridden()
        {
            Injector
                .Setup(i => i.Get(typeof(IRequestContext), null))
                .Returns(new RequestContext("cica", nameof(IModule), nameof(IModule.DoSomething), null, default));

            BeginScope
                .Setup(fn => fn(It.Is<Dictionary<string, object>>(d => d["Module"].ToString() == nameof(IModule) && d["Method"].ToString() == nameof(IModule.DoSomething) && d["SessionId"].ToString() == "cica")))
                .Returns(new Disposable());

            Type proxyType = ProxyGenerator<IModule, Logger<IModule>>.GetGeneratedType();

            IModule module = (IModule)Activator.CreateInstance(proxyType, Module.Object, Injector.Object, new LogForwarder(BeginScope.Object, Log.Object));
            Assert.DoesNotThrow(() => module.DoSomethingElse());
        }

        [Test]
        public void ExceptionLogger_ShouldLog()
        {
            Module
                .Setup(m => m.DoSomething(It.IsAny<string>(), It.IsAny<object>()))
                .Throws(new Exception("This is the message"));

            Injector
                .Setup(i => i.Get(typeof(IRequestContext), null))
                .Returns(new RequestContext("cica", nameof(IModule), nameof(IModule.DoSomething), null, default));

            int callOrder = 0;

            BeginScope
                .Setup(fn => fn(It.Is<Dictionary<string, object>>(d => d["Module"].ToString() == nameof(IModule) && d["Method"].ToString() == nameof(IModule.DoSomething) && d["SessionId"].ToString() == "cica")))
                .Returns<Dictionary<string, object>>(_ =>
                {
                    Assert.That(callOrder++, Is.EqualTo(0));
                    return new Disposable();
                });
            Log
                .Setup(fn => fn(LogLevel.Information, default, $"Parameters: arg1:cica,{Environment.NewLine}arg2:1"))
                .Callback<LogLevel, EventId, string>((_, _, _) => Assert.That(callOrder++, Is.EqualTo(1)));
            Log
                .Setup(fn => fn(LogLevel.Information, default, It.Is<string>(s => s.StartsWith("Time elapsed:"))))
                .Callback<LogLevel, EventId, string>((_, _, _) => Assert.That(callOrder++, Is.EqualTo(2)));
            Log
                .Setup(fn => fn(LogLevel.Error, default, It.Is<string>(s => s.StartsWith("Unhandled exception: System.Exception: This is the message"))))
                .Callback<LogLevel, EventId, string>((_, _, _) => Assert.That(callOrder++, Is.EqualTo(3)));

            Type proxyType = ProxyGenerator<IModule, Logger<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, Module.Object, Injector.Object, new LogForwarder(BeginScope.Object, Log.Object));
            Assert.Throws<Exception>(() => module.DoSomething("cica", 1));
            Assert.That(callOrder, Is.EqualTo(4));
        }
    }
}
