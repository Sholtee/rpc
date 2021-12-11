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
        [Verb("install")]
        public virtual void OnInstall() { }

        /// <summary>
        /// Called on app uninstall.
        /// </summary>
        [Verb("uninstall")]
        public virtual void OnUnInstall() { }
    }
}
