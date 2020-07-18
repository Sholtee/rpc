/********************************************************************************
* ServiceHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;
using System.IO;

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
            InvokeExe(Path.ChangeExtension(typeof(ICalculator).Assembly.Location, "exe"), "-install");
            InvokeExe("sc", "start Calculator");
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            InvokeExe("sc", "stop Calculator");
            InvokeExe(Path.ChangeExtension(typeof(ICalculator).Assembly.Location, "exe"), "-uninstall");
        }

        [Test]
        public void InstalledService_ShouldRun() 
        {
            using var client = new RpcClient<ICalculator>("http://127.0.0.1:1986/api/");

            Assert.That(client.Proxy.Add(1, 1), Is.EqualTo(2));
        }

        private static void InvokeExe(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe)
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
