/********************************************************************************
* ConsoleHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Interfaces;
    using Internals;
    
    [TestFixture]
    public class ConsoleHostRunnerTests
    {
        private class BadDependencyAppHost : AppHostBase
        {
            public override string Name => throw new NotImplementedException();

            public override string Url => throw new NotImplementedException();

            public BadDependencyAppHost() : base() 
            {
                Dependencies.Add("invalid");
            }
        }

        [Test]
        public void Start_ShouldValidateTheDependencies()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            using IHost appHost = new BadDependencyAppHost();
            using IHostRunner hostRunner = new ConsoleHostRunner(appHost);

            Assert.Throws<Exception>(hostRunner.Start);
        }

        private class ConsoleAppHost : AppHostBase
        {
            public override string Name => nameof(ConsoleAppHost);

            public override string Url => "http://127.0.0.1:1986/api/";

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();

            public override void OnStart()
            {
                base.OnStart();
                Started.Set();
            }

            public ConsoleAppHost() : base()
            {
                Dependencies.Add("LanmanWorkstation");
            }

            protected override void Dispose(bool disposeManaged)
            {
                if (disposeManaged)
                    Started.Dispose();

                base.Dispose(disposeManaged);
            }
        }

        [Test]
        public void Runner_ShouldTerminateOnCtrlC() 
        {
            using ConsoleAppHost appHost = new ConsoleAppHost();
            using ConsoleHostRunner hostRunner = new ConsoleHostRunner(appHost);

            Task t = Task.Factory.StartNew(((IHostRunner) hostRunner).Start);
            Assert.That(appHost.Started.Wait(TimeSpan.FromSeconds(1)));

            hostRunner.OnConsoleCancel(null, null);

            Assert.That(t.Wait(TimeSpan.FromSeconds(1)));
        }
    }
}
