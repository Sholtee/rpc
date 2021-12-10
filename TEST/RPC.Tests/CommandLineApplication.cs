/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using Internals;

    [TestFixture]
    public class CommandLineApplicationTests
    {
        public class SimpleCommandLineApplication : CommandLineApplication
        {
            public SimpleCommandLineApplication() : base(Array.Empty<string>())
            {
            }
        }

        [Test]
        public void Run_ShouldInvokeTheOnRunMethodByDefault()
        {
            Mock<SimpleCommandLineApplication> mockApp = new(MockBehavior.Strict);
            mockApp.Setup(x => x.OnRun());

            mockApp.Object.Run();

            mockApp.Verify(x => x.OnRun(), Times.Once);
        }

        public class SimpleCommandLineApplicationUsingSimpleVerb : CommandLineApplication
        {
            public SimpleCommandLineApplicationUsingSimpleVerb(IReadOnlyList<string> args) : base(args)
            {
            }

            private sealed class InstallOpts
            {
                public string User { get; set; }
                public string Password { get; set; }
            }

            private void ValidateArgs()
            {
                var opts = GetParsedArguments<InstallOpts>();

                Assert.That(opts, Is.Not.Null);
                Assert.That(opts.User, Is.EqualTo("cica"));
                Assert.That(opts.Password, Is.EqualTo("mica"));
            }

            [Verb("install")]
            public virtual void OnInstall() => ValidateArgs();

            [Verb("install", "service")]
            public virtual void OnInstallService() => ValidateArgs();

            [Verb("uninstall")]
            public virtual void OnUninstall()
            {
            }
        }

        [Test]
        public void Run_ShouldInvokeTheMethodHavingTheAppropriateVerb()
        {
            Mock<SimpleCommandLineApplicationUsingSimpleVerb> mockApp = new(MockBehavior.Strict, new object[] { new string[] { "install", "-user", "cica", "--password", "mica", "-unused" } });
            mockApp.Setup(x => x.OnInstall()).CallBase();

            mockApp.Object.Run();

            mockApp.Verify(x => x.OnInstall(), Times.Once);
        }

        [Test]
        public void Run_ShouldInvokeTheMethodHavingTheAppropriateVerbs()
        {
            Mock<SimpleCommandLineApplicationUsingSimpleVerb> mockApp = new(MockBehavior.Strict, new object[] { new string[] { "install", "service", "-user", "cica", "--password", "mica", "-unused" } });
            mockApp.Setup(x => x.OnInstallService()).CallBase();

            mockApp.Object.Run();

            mockApp.Verify(x => x.OnInstallService(), Times.Once);
        }
    }
}
