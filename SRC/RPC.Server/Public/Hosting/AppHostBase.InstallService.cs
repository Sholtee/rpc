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
                Verb            = "runas",
                CreateNoWindow  = true,
                WindowStyle     = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process sc = Process.Start(psi);

            sc.WaitForExit();

            if (sc.ExitCode is not 0)
            {
                InvalidOperationException ex = new(Errors.SC_INVOCATION_FAILED);
                ex.Data[nameof(sc.ExitCode)] = sc.ExitCode;
                throw ex;
            }
        }

        private static string GetSafeServiceName(Win32ServiceDescriptor serviceDescriptor) => serviceDescriptor.Name.Replace(' ', '_');

        #pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant
        [Verb("service", "install")]
        #pragma warning restore CS3016
        internal void OnInstallWin32Service()
        {
            //
            // Ha az alap telepites sikeres csak akkor telepitjuk a Win32 szervizt.
            //

            OnInstall();

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
        }

        #pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant
        [Verb("service", "uninstall")]
        #pragma warning restore CS3016
        internal void OnUnInstallWin32Service()
        {
            OnUnInstall();

            Win32ServiceDescriptor serviceDescriptor = new();
            OnConfigureWin32Service(serviceDescriptor);

            InvokeScm($"delete {GetSafeServiceName(serviceDescriptor)}");
        }
        #endregion

        /// <summary>
        /// Called on service install/uninstall.
        /// </summary>
        /// <remarks>Override this method to configure the Win32 service being installed.</remarks>
        public virtual void OnConfigureWin32Service(Win32ServiceDescriptor descriptor) {}
    }
}
