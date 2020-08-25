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
    using Interfaces;
    using Internals;

    [TestFixture]
    public class InstallHostRunnerTests
    {
        private class AppHost : AppHostBase
        {
            public override string Name => "MyService";

            public override string Url => throw new NotImplementedException();

            public AppHost() : base() => Dependencies.Add("LanmanWorkstation");
        }

        private static void InvokeRunner(HostConfiguration configuration, bool install = false, bool uninstall = false) 
        {
            using IHost appHost = new AppHost();
            using IHostRunner hostRunner = new InstallHostRunner_WinNT(appHost, configuration) 
            {
                Install = install,
                Uninstall = uninstall
            };
            hostRunner.Start();
        }

        [Test]
        public void Install_ShouldInstallTheService([Values(HostConfiguration.Debug, HostConfiguration.Release)] HostConfiguration configuration)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            InvokeRunner(configuration, install: true);

            try
            {
                ServiceController svc = ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService");

                Assert.That(svc, Is.Not.Null);
                Assert.That(svc.ServicesDependedOn.Any(svc => svc.ServiceName == "LanmanWorkstation"));
            }
            finally 
            {
                InvokeRunner(configuration, uninstall: true);
            }
        }

        [Test]
        public void Uninstall_ShouldUninstallTheService([Values(HostConfiguration.Debug, HostConfiguration.Release)] HostConfiguration configuration)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            try
            {
                InvokeRunner(configuration, install: true);
            }

            //
            // Ha mar telepitve volt korabban
            //

            catch { }

            InvokeRunner(configuration, uninstall: true);

            ServiceController svc = ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService");
            Assert.That(svc, Is.Null);
        }
    }
}
