/********************************************************************************
* ModulesExtensions.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc
{
    using Interfaces;
    using Internals;
    using Pipeline;

    /// <summary>
    /// Defines some extenions against the <see cref="Modules"/> class. 
    /// </summary>
    public static class ModulesExtensions 
    {
        /// <summary>
        /// Installs the built-in modules.
        /// </summary>
        /// <remarks>Currently this method installs the <see cref="IServiceDescriptor"/> module only.</remarks>
        public static Modules InstallBuiltInModules(this Modules modules)
        {
            if (modules is null)
                throw new ArgumentNullException(nameof(modules));

            return modules.Register<IServiceDescriptor, ServiceDescriptor>();
        }
    }
}
