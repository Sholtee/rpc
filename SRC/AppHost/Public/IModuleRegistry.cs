/********************************************************************************
* IModuleRegistry.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.AppHost
{
    /// <summary>
    /// Defines an abstract service registry.
    /// </summary>
    public interface IModuleRegistry
    {
        /// <summary>
        /// Registers a module.
        /// </summary>
        public void Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface;
    }
}
