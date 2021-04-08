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
                #pragma warning disable CA2201 // To preserve backward compatibility keep throwing simple Exception only
                throw new Exception(string.Format(Errors.Culture, Errors.SC_INVOCATION_FAILED, netsh.ExitCode));
                #pragma warning restore CA2201
        }

        private string GetSafeServiceName() => Host.Name.Replace(' ', '_');

        private void DeleteService() => InvokeScm($"delete {GetSafeServiceName()}");

        private void InstallService() 
        {
            var sb = new StringBuilder($"create {GetSafeServiceName()} binPath= \"{Process.GetCurrentProcess().MainModule.FileName}\" start= {(Host.AutoStart ? "auto" : "demand")}");

            if (Host.Description is not null)
                sb.Append($" displayname= \"{Host.Description}\"");
            if (Host.Dependencies.Any())
                sb.Append($" depend= {string.Join("/", Host.Dependencies)}");

            InvokeScm(sb.ToString());
        }

        internal bool Install 
        { 
            get;
            init;
        }

        internal bool Uninstall 
        { 
            get;
            init;
        }

        internal InstallHostRunner_WinNT(IHost host, HostConfiguration configuration) : base(host, configuration) { }
        #endregion

        public override void Start()
        {
            if (Install)
            {
                InstallService();

                try
                {
                    Host.OnInstall();
                }
                catch 
                {
                    DeleteService();
                    throw;
                }
            }

            if (Uninstall) 
            {
                Host.OnUninstall();
                DeleteService();
            }          
        }

        public override void Stop() {}

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
