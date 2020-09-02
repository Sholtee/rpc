/********************************************************************************
* ServiceHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Server.Sample;

    [TestFixture]
    public class ServiceHostRunnerTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            InvokeSc($"create Calculator binPath= \"%ProgramFiles%\\dotnet\\dotnet.exe {typeof(ICalculator).Assembly.Location}\"");
            InvokeSc("start Calculator");
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            InvokeSc("stop Calculator");
            InvokeSc("delete Calculator");
        }

        [Test]
        public async Task InstalledService_ShouldRun() 
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            using var factory = new RpcClientFactory("http://localhost:1986/api/");
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
