/********************************************************************************
* InstallHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.ServiceProcess;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Interfaces;
    using Internals;

    [TestFixture]
    public class InstallHostRunnerTests
    {
        public class AppHost : AppHostBase
        {
            public sealed override string Name => "MyService";

            public override string Url => throw new NotImplementedException();

            public AppHost() : base() => Dependencies.Add("LanmanWorkstation");
        }

        private static void InvokeRunner(IHost appHost, HostConfiguration configuration, bool install = false, bool uninstall = false) 
        {
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

            var mockAppHost = new Mock<AppHost>(MockBehavior.Loose);

            InvokeRunner(mockAppHost.Object, configuration, install: true);

            try
            {
                mockAppHost.Verify(h => h.OnInstall(), Times.Once);
                mockAppHost.Verify(h => h.OnStart(It.IsAny<HostConfiguration>()), Times.Never);

                ServiceController svc = ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService");

                Assert.That(svc, Is.Not.Null);
                Assert.That(svc.ServicesDependedOn.Any(svc => svc.ServiceName == "LanmanWorkstation"));
            }
            finally 
            {
                InvokeRunner(mockAppHost.Object, configuration, uninstall: true);
            }
        }

        [Test]
        public void Install_ShouldRevertTheServiceInstallationOnError([Values(HostConfiguration.Debug, HostConfiguration.Release)] HostConfiguration configuration)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            var ex = new Exception();

            var mockAppHost = new Mock<AppHost>(MockBehavior.Loose);
            mockAppHost
                .Setup(h => h.OnInstall())
                .Callback(() => 
                {
                    Assert.That(ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService"), Is.Not.Null);
                    throw ex;
                });

            Exception bubbled = Assert.Throws<Exception>(() => InvokeRunner(mockAppHost.Object, configuration, install: true));
            Assert.AreSame(ex, bubbled);
            Assert.That(ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService"), Is.Null);
        }

        [Test]
        public void Uninstall_ShouldUninstallTheService([Values(HostConfiguration.Debug, HostConfiguration.Release)] HostConfiguration configuration)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            try
            {
                InvokeRunner(new AppHost(), configuration, install: true);
            }

            //
            // Ha mar telepitve volt korabban
            //

            catch { }

            var mockAppHost = new Mock<AppHost>(MockBehavior.Loose);

            InvokeRunner(mockAppHost.Object, configuration, uninstall: true);

            mockAppHost.Verify(h => h.OnUninstall(), Times.Once);
            mockAppHost.Verify(h => h.OnStop(), Times.Never);

            ServiceController svc = ServiceController.GetServices().SingleOrDefault(svc => svc.ServiceName == "MyService");
            Assert.That(svc, Is.Null);
        }

        [Test]
        public void Install_ShouldExecuteCustomInstallLogic([Values(HostConfiguration.Debug, HostConfiguration.Release)] HostConfiguration configuration)
        {
            var mockAppHost = new Mock<AppHost>(MockBehavior.Loose);

            using IHostRunner hostRunner = new InstallHostRunner(mockAppHost.Object, configuration)
            {
                Install = true
            };
            hostRunner.Start();

            mockAppHost.Verify(h => h.OnInstall(), Times.Once);
            mockAppHost.Verify(h => h.OnStart(It.IsAny<HostConfiguration>()), Times.Never);
        }

        [Test]
        public void Uninstall_ShouldExecuteCustomUninstallLogic([Values(HostConfiguration.Debug, HostConfiguration.Release)] HostConfiguration configuration)
        {
            var mockAppHost = new Mock<AppHost>(MockBehavior.Loose);

            using IHostRunner hostRunner = new InstallHostRunner(mockAppHost.Object, configuration)
            {
                Uninstall = true
            };
            hostRunner.Start();

            mockAppHost.Verify(h => h.OnUninstall(), Times.Once);
            mockAppHost.Verify(h => h.OnStop(), Times.Never);
        }
    }
}
