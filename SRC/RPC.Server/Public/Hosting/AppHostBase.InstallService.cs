/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Solti.Utils.Rpc.Hosting
{
    using Properties;
    using Rpc.Internals;

    public partial class AppHostBase
    {
        #region Private
        private static void InvokeScm(string arguments)
        {
            ProcessStartInfo psi = new("sc", arguments)
            {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process netsh = Process.Start(psi);

            netsh.WaitForExit();

            if (netsh.ExitCode is not 0)
                #pragma warning disable CA2201 // To preserve backward compatibility keep throwing simple Exception only
                throw new Exception(string.Format(Errors.Culture, Errors.SC_INVOCATION_FAILED, netsh.ExitCode));
                #pragma warning restore CA2201
        }

        private static string GetSafeServiceName(Win32ServiceDescriptor serviceDescriptor) => serviceDescriptor.Name.Replace(' ', '_');

        #pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant
        [Verb("service", "install")]
        #pragma warning restore CS3016
        internal void OnInstallWin32Service()
        {
            Win32ServiceDescriptor serviceDescriptor = new();
            OnConfigureWin32Service(serviceDescriptor);

            //
            // Itt az a trukk hogy az app a "service" parancssori argumentumbol fogja tudni hogy szervizkent van
            // inditva.
            //

            StringBuilder sb = new($"create {GetSafeServiceName(serviceDescriptor)} binPath= \"{Process.GetCurrentProcess().MainModule.FileName} service\" start= {(serviceDescriptor.AutoStart ? "auto" : "demand")}");

            if (serviceDescriptor.Description is not null)
                sb.Append($" displayname= \"{serviceDescriptor.Description}\"");
            if (serviceDescriptor.Dependencies.Any())
                sb.Append($" depend= {string.Join("/", serviceDescriptor.Dependencies)}");

            InvokeScm(sb.ToString());

            OnInstall();
        }

        #pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant
        [Verb("service", "uninstall")]
        #pragma warning restore CS3016
        internal void OnUnInstallWin32Service()
        {
            Win32ServiceDescriptor serviceDescriptor = new();
            OnConfigureWin32Service(serviceDescriptor);

            InvokeScm($"delete {GetSafeServiceName(serviceDescriptor)}");

            OnUnInstall();
        }
        #endregion

        /// <summary>
        /// Called on service install/uninstall.
        /// </summary>
        /// <remarks>Override this method to configure the Win32 service being installed.</remarks>
        public virtual void OnConfigureWin32Service(Win32ServiceDescriptor descriptor) {}
    }
}
