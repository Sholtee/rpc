/********************************************************************************
* InstallHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    using Interfaces;

    internal class InstallHostRunner : HostRunner
    {
        #region Private
        internal bool Install
        {
            get;
#if DEBUG
            set;
#endif
        }

        internal bool Uninstall
        {
            get;
#if DEBUG
            set;
#endif
        }

        internal InstallHostRunner(IHost host, HostConfiguration configuration) : base(host, configuration) { }
        #endregion

        public override void Start()
        {
            if (Install)
                Host.OnInstall();

            if (Uninstall) 
                Host.OnUninstall();
        }

        public override void Stop()
        {
        }

        #region Factory
        private sealed class FactoryImpl : IHostRunnerFactory
        {
            private bool Install { get; } = ArgSet("-INSTALL");

            private bool Uninstall { get; } = ArgSet("-UNINSTALL");

            private static bool ArgSet(string name) => Environment
                .GetCommandLineArgs()
                .Any(arg => arg.ToUpperInvariant() == name);

            public bool IsCompatible(IHost host) =>
                Environment.UserInteractive &&
                (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || ArgSet("-NOSERVICE")) &&
                (Install || Uninstall);

            public IHostRunner CreateRunner(IHost host, HostConfiguration configuration) => new InstallHostRunner(host, configuration) 
            {
                Install   = Install,
                Uninstall = Uninstall
            };
        }

        public static IHostRunnerFactory Factory { get; } = new FactoryImpl();
        #endregion
    }
}
