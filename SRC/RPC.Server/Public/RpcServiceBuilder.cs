/********************************************************************************
* RpcServiceBuilder.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace Solti.Utils.Rpc
{
    using DI;
    using DI.Interfaces;

    using Interfaces;
    using Internals;

    /// <summary>
    /// Builds <see cref="RpcService"/> instances.
    /// </summary>
    public class RpcServiceBuilder
    {
        internal const string META_REQUEST = nameof(META_REQUEST);

        private readonly ModuleRegistry FModuleRegistry;
        private readonly ServiceCollection FServiceCollection;
        private WebServiceDescriptor FWebServiceDescriptor = new();

        private sealed class MetaReaderServiceEntry : AbstractServiceEntry
        {
            private object? FInstance;

            private MetaReaderServiceEntry(MetaReaderServiceEntry original, IServiceRegistry owner) : base(original.Interface, original.Name, null, owner)
            {
                MetaName = original.MetaName;
                State = ServiceEntryStates.Built;
            }

            public MetaReaderServiceEntry(Type @interface, string metaName) : base(@interface, null, null, null) => MetaName = metaName;

            public string MetaName { get; }

            public override object CreateInstance(IInjector scope) => throw new InvalidOperationException();

            public override object GetSingleInstance()
            {
                if (FInstance is null)
                {
                    IInjector injector = (IInjector) Owner!;
                    FInstance = injector.Meta(MetaName)!;
                }
                return FInstance;
            }

            public override AbstractServiceEntry CopyTo(IServiceRegistry owner) => new MetaReaderServiceEntry(this, owner);
        }

        private sealed class ModuleRegistry : ModuleInvocationBuilder, IModuleRegistry
        {
            public ModuleRegistry(IServiceCollection serviceCollection) => ServiceCollection = serviceCollection;

            public IServiceCollection ServiceCollection { get; }

            public IModuleRegistry Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface
            {
                ServiceCollection.Service<TInterface, TImplementation>(Lifetime.Scoped);
                AddModule<TInterface>();
                return this;
            }

            public IModuleRegistry Register<TInterface>(Func<IInjector, TInterface> factory) where TInterface : class
            {
                ServiceCollection.Factory<TInterface>(factory, Lifetime.Scoped);
                AddModule<TInterface>();
                return this;
            }
        }

        /// <summary>
        /// Creates a new <see cref="RpcServiceBuilder"/> instance.
        /// </summary>
        public RpcServiceBuilder()
        {
            FServiceCollection = new ServiceCollection();
            FServiceCollection
                .Register(new MetaReaderServiceEntry(typeof(IRequestContext), META_REQUEST))
                .Factory<IReadOnlyList<string>>("CommandLineArgs", _ => Environment.GetCommandLineArgs(), Lifetime.Singleton)
                .Factory<IDictionary>("EnvironmentVariables", _ => Environment.GetEnvironmentVariables(), Lifetime.Singleton);

            FModuleRegistry = new ModuleRegistry(FServiceCollection);
        }

        /// <summary>
        /// 
        /// </summary>
        public RpcServiceBuilder ConfigureModules(Action<IModuleRegistry> configDelegate)
        {
            if (configDelegate is null)
                throw new ArgumentNullException(nameof(configDelegate));

            configDelegate(FModuleRegistry);

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        public RpcServiceBuilder ConfigureServices(Action<IServiceCollection> configDelegate)
        {
            if (configDelegate is null)
                throw new ArgumentNullException(nameof(configDelegate));

            configDelegate(FServiceCollection);

            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        public RpcServiceBuilder ConfigureWebService(WebServiceDescriptor descriptor)
        {
            FWebServiceDescriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        public RpcServiceBuilder ConfigureSerializer(JsonSerializerOptions serializerOptions)
        {
            FModuleRegistry.SerializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
            return this;
        }

        /// <summary>
        /// Builds a new <see cref="RpcService"/> instance.
        /// </summary>
        public virtual RpcService Build() => new RpcService(FWebServiceDescriptor, ScopeFactory.Create(FServiceCollection), FModuleRegistry.Build(), FModuleRegistry.SerializerOptions);
    }
}
