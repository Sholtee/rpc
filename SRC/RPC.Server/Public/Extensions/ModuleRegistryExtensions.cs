/********************************************************************************
* ModuleRegistryExtensions.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc
{
    using Interfaces;
    using Internals;

    /// <summary>
    /// Defines some extenions against the <see cref="IModuleRegistry"/> interface. 
    /// </summary>
    public static class ModuleRegistryExtensions 
    {
        /// <summary>
        /// Installs the built-in modules.
        /// </summary>
        /// <remarks>Currently this method installs the <see cref="IServiceDescriptor"/> module only.</remarks>
        public static IModuleRegistry InstallBuiltInModules(this IModuleRegistry registry)
        {
            if (registry is null)
                throw new ArgumentNullException(nameof(registry));

            return registry.Register<IServiceDescriptor, ServiceDescriptor>();
        }
    }
}
