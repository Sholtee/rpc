/********************************************************************************
* InstallHostRunner_WinNT.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Solti.Utils.Rpc.Hosting.Internals
{
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
                throw new Exception(string.Format(Resources.Culture, Resources.SC_INVOCATION_FAILED, netsh.ExitCode));
        }

        internal bool Install { get; set; }

        internal bool Uninstall { get; set; }

        internal InstallHostRunner_WinNT(IHost host) : base(host) { }
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
            private bool Install { get; } = ArgSet("-install");

            private bool Uninstall { get; } = ArgSet("-uninstall");

            private static bool ArgSet(string name) => Environment
                .GetCommandLineArgs()
                .Any(arg => arg.ToLower(Resources.Culture ?? CultureInfo.CurrentCulture) == name);

            public bool ShouldUse => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && Install || Uninstall;

            public IHostRunner CreateRunner(IHost host) => new InstallHostRunner_WinNT(host) 
            {
                Install   = Install,
                Uninstall = Uninstall
            };
        }

        public static IHostRunnerFactory Factory { get; } = new FactoryImpl();
        #endregion
    }
}
