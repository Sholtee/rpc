/********************************************************************************
* ServiceHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Server.Sample.Interfaces;

    [TestFixture]
    public class ServiceHostRunnerTests
    {
        const string HOST = "http://localhost:1986/api/";

        [OneTimeSetUp]
        public async Task Setup()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;

            InvokeSc($"create Calculator binPath= \"%ProgramFiles%\\dotnet\\dotnet.exe {typeof(ICalculator).Assembly.Location}\"");
            InvokeSc("start Calculator");

            using var factory = new RpcClientFactory(HOST);
            int attempts = 0;
            do
            {
                try
                {
                    await factory.ServiceVersion;
                    break;          
                }
                catch 
                {
                    if (++attempts == 5) throw;
                    Thread.Sleep(50);
                }
            }
            while (true);
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;

            InvokeSc("stop Calculator");
            InvokeSc("delete Calculator");
        }

        [Test]
        public async Task InstalledService_ShouldRun() 
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            using var factory = new RpcClientFactory(HOST);
            ICalculator calculator = await factory.CreateClient<ICalculator>();

            Assert.That(await calculator.AddAsync(1, 1), Is.EqualTo(2));
        }

        private static void InvokeSc(string args)
        {
            var psi = new ProcessStartInfo("sc")
            {
                Verb = "runas",
                Arguments = args,
                UseShellExecute = true
            };

            Process proc = Process.Start(psi);
            proc.WaitForExit();

            Assert.That(proc.ExitCode, Is.EqualTo(0));
        }
    }
}
