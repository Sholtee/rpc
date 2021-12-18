/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Hosting
{
    using Rpc.Internals;

    public partial class AppHostBase
    {
        /// <summary>
        /// Called on app install.
        /// </summary>
        /// <remarks>Override this method to implement app specific installation logic.</remarks>
        [Verb("install")]
        public virtual void OnInstall() { }

        /// <summary>
        /// Called on app uninstall.
        /// </summary>
        /// <remarks>Override this method to implement app specific uninstallation logic.</remarks>
        [Verb("uninstall")]
        public virtual void OnUnInstall() { }
    }
}
