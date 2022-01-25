/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Hosting
{
    using DI;
    using DI.Interfaces;
    using Internals;

    public partial class AppHostBase
    {
        /// <summary>
        /// Invokes an action in a separate scope.
        /// </summary>
        protected void InvokeInScope(Action<IInjector> invocation)
        {
            if (invocation is null)
                throw new ArgumentNullException(nameof(invocation));

            using IInjector root = (IInjector) ScopeFactory.Create(OnConfigureServices); // TODO: ScopeOptions is be lehesssen allitani
            invocation(root);
        }

        /// <summary>
        /// Called on app install.
        /// </summary>
        /// <remarks>Override this method to implement app specific installation logic.</remarks>
        [Verb("install")]
        public virtual void OnInstall() => InvokeInScope(OnInstall);

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
        public virtual void OnUnInstall() => InvokeInScope(OnUnInstall);

        /// <summary>
        /// Called on app uninstall.
        /// </summary>
        /// <remarks>Override this method to implement app specific uninstallation logic.</remarks>
        public virtual void OnUnInstall(IInjector scope) { }
    }
}
