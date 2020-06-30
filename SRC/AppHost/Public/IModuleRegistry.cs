/********************************************************************************
* IModuleRegistry.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.AppHost
{
    /// <summary>
    /// Defines an abstract module registry.
    /// </summary>
    public interface IModuleRegistry
    {
        /// <summary>
        /// Registers a module.
        /// </summary>
        void Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface;
    }
}
