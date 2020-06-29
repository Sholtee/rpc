/********************************************************************************
* ModuleRegistry.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.AppHost.Internals
{
    using DI;
    using DI.Interfaces;

    internal sealed class ModuleRegistry : IModuleRegistry
    {
        public IServiceContainer Container { get; }

        public ModuleRegistry(IServiceContainer container) => Container = container;

        public void Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface => Container.Service<TInterface, TImplementation>(Lifetime.Scoped);
    }
}
