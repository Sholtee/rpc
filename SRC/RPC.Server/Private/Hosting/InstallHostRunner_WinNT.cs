/********************************************************************************
* InstallHostRunner_WinNT.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    using Interfaces;
    using Properties;

    internal class InstallHostRunner_WinNT : HostRunner
    {
        #region Private
        private static void InvokeScm(string arguments)
        {
            var psi = new ProcessStartInfo("sc", arguments)
            {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process netsh = Process.Start(psi);

            netsh.WaitForExit();

            if (netsh.ExitCode != 0)
                throw new Exception(string.Format(Errors.Culture, Errors.SC_INVOCATION_FAILED, netsh.ExitCode));
        }

        internal bool Install { get; set; }

        internal bool Uninstall { get; set; }

        internal InstallHostRunner_WinNT(IHost host, HostConfiguration configuration) : base(host, configuration) { }
        #endregion

        public override void Start()
        {
            if (Install)
            {
                var sb = new StringBuilder($"create {GetSafeServiceName()} binPath= \"{Process.GetCurrentProcess().MainModule.FileName}\" start= {(Host.AutoStart ? "auto" : "demand")}");
                
                if (Host.Description != null)
                    sb.Append($" displayname= \"{Host.Description ?? string.Empty}\"");
                if (Host.Dependencies.Any())
                    sb.Append($" depend= {string.Join("/", Host.Dependencies)}");

                InvokeScm(sb.ToString());
                Host.OnInstall();
            }

            if (Uninstall) 
            {
                InvokeScm($"delete {GetSafeServiceName()}");
                Host.OnUninstall();
            }

            string GetSafeServiceName() => Host.Name.Replace(' ', '_');
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

            public bool IsCompatible(IHost host) => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && (Install || Uninstall);

            public IHostRunner CreateRunner(IHost host, HostConfiguration configuration) => new InstallHostRunner_WinNT(host, configuration) 
            {
                Install   = Install,
                Uninstall = Uninstall
            };
        }

        public static IHostRunnerFactory Factory { get; } = new FactoryImpl();
        #endregion
    }
}
