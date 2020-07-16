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
            {
                Console.Error.WriteLine(string.Format(Resources.Culture, Resources.SC_INVOCATION_FAILED, netsh.ExitCode));
                Environment.Exit(netsh.ExitCode);
            }
        }

        private bool Install { get; }

        private bool Uninstall { get; }
        #endregion

        public InstallHostRunner_WinNT(IHost host): base(host) 
        {
            Install = ArgSet("-install");
            Uninstall = ArgSet("-uninstall");

            static bool ArgSet(string name) => Environment.GetCommandLineArgs().Any(arg => arg.ToLower(Resources.Culture) == name);
        }

        protected override void Start()
        {
            if (Install)
            {
                var sb = new StringBuilder($"create {GetSafeServiceName()} binPath= \"{Host.GetType().Assembly.Location}\" start= {(Host.AutoStart ? "auto" : "demand")}");
                
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

        protected override void Stop()
        {
        }

        public override bool ShouldUse => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && Install || Uninstall;
    }
}
