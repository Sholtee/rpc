/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Hosting
{
    using DI;
    using DI.Interfaces;
    using Rpc.Internals;

    public partial class AppHostBase
    {
        /// <summary>
        /// Called on app install.
        /// </summary>
        /// <remarks>Override this method to implement app specific installation logic.</remarks>
        [Verb("install")]
        public virtual void OnInstall()
        {
            using IScopeFactory root = ScopeFactory.Create(OnCofigureServices); // TODO: ScopeOptions is be lehesssen allitani
            OnInstall((IInjector) root);
        }

        /// <summary>
        /// Called on app install.
        /// </summary>
        /// <remarks>Override this method to implement app specific installation logic.</remarks>
        public virtual void OnInstall(IInjector scope) { }

        /// <summary>
        /// Called on app uninstall.
        /// </summary>
        /// <remarks>Override this method to implement app specific uninstallation logic.</remarks>
        [Verb("uninstall")]
        public virtual void OnUnInstall()
        {
            using IScopeFactory root = ScopeFactory.Create(OnCofigureServices); // TODO: ScopeOptions is be lehesssen allitani
            OnUnInstall((IInjector) root);
        }

        /// <summary>
        /// Called on app uninstall.
        /// </summary>
        /// <remarks>Override this method to implement app specific uninstallation logic.</remarks>
        public virtual void OnUnInstall(IInjector scope) { }
    }
}
