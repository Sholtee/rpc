/********************************************************************************
* InstallHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.ServiceProcess;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Internals;

    [TestFixture]
    public class InstallHostRunnerTests
    {
        private class AppHost : AppHostBase
        {
            public override string Name => "MyService";

            public override string Url => throw new NotImplementedException();

            public AppHost() : base() 
            {
                Dependencies.Add("LanmanWorkstation");
            }
        }

        [Test]
        public void Install_ShouldInstallTheService()
        {
            using IHost appHost = new AppHost();
            using IHostRunner hostRunner = new InstallHostRunner_WinNT(appHost, new[] { "-install" });
            hostRunner.Start();

            ServiceController svc = ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService");

            Assert.That(svc, Is.Not.Null);
            Assert.That(svc.ServicesDependedOn.Any(svc => svc.ServiceName == "LanmanWorkstation"));
        }

        [Test]
        public void Uninstall_ShouldUninstallTheService()
        {
            try
            {
                Install_ShouldInstallTheService();
            }
            catch { }

            using IHost appHost = new AppHost();
            using IHostRunner hostRunner = new InstallHostRunner_WinNT(appHost, new[] { "-uninstall" });
            hostRunner.Start();

            ServiceController svc = ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService");
            Assert.That(svc, Is.Null);
        }
    }
}
