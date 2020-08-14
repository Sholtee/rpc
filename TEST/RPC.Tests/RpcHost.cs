/********************************************************************************
* RpcHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;
using System.IO;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using Interfaces;
    using Server.Sample;
    
    [TestFixture]
    public class RpcHostTests
    {
        const string Host = "http://localhost:1986/api/";

        public Process HostProcess { get; private set; }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                Arguments = Path.ChangeExtension(typeof(ICalculator).Assembly.Location, "dll"),
                UseShellExecute = true
            };

            HostProcess = Process.Start(psi);
        }

        [OneTimeTearDown]
        public void OneTimeCleanup()
        {
            HostProcess?.Kill();
            HostProcess = null;
        }

        public interface IDummy { }

        [Test]
        public void Client_ShouldGetTheServiceVersion()
        {
            using var clientFactory = new RpcClientFactory(Host);

            //
            // Mivel a kiszolgalo "dotnet.exe"-vel lett inditva es a verzioinfo mindig a host exe verzioja
            // ezert ez most it gyakorlatilag a runtime verziojat kene visszaadja.
            //

            Version
                serverVer = clientFactory.ServiceVersion,
                corVer = HostProcess.MainModule.FileVersionInfo;

            Assert.That(serverVer, Is.EqualTo(corVer));
        }
    }
}
