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

        public InstallHostRunner_WinNT() 
        {
            Install = ArgSet("-install");
            Uninstall = ArgSet("-uninstall");

            static bool ArgSet(string name) => Environment.GetCommandLineArgs().Any(arg => arg.ToLower(Resources.Culture) == name);
        }

        public override void Run(AppHostBase appHost)
        {
            if (Install)
            {
                var sb = new StringBuilder($"create {GetSafeServiceName()} binPath= \"{appHost.GetType().Assembly.Location}\" start= {(appHost.AutoStart ? "auto" : "demand")}");
                
                if (appHost.Description != null)
                    sb.Append($" displayname= \"{appHost.Description ?? string.Empty}\"");
                if (appHost.Dependencies.Any())
                    sb.Append($" depend= {string.Join("/", appHost.Dependencies)}");

                InvokeScm(sb.ToString());
                appHost.OnInstall();
            }

            if (Uninstall) 
            {
                InvokeScm($"delete {GetSafeServiceName()}");
                appHost.OnUninstall();
            }

            string GetSafeServiceName() => appHost.Name.Replace(' ', '_');
        }

        public override bool ShouldUse() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && Install || Uninstall;
    }
}
