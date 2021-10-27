/********************************************************************************
* IModuleRegistry.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Describes an abstract module registry.
    /// </summary>
    public interface IModuleRegistry
    {
        /// <summary>
        /// Registers a module.
        /// </summary>
        IModuleRegistry Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface;

        /// <summary>
        /// Registers a module.
        /// </summary>
        IModuleRegistry Register<TInterface>(Func<IInjector, TInterface> factory) where TInterface : class;
    }
}
