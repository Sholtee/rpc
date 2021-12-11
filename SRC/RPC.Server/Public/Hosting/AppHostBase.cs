/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Hosting
{
    using Rpc.Interfaces;
    using Rpc.Internals;

    /// <summary>
    /// Represents the base class of the host application.
    /// </summary>
    public partial class AppHostBase: CommandLineApplication
    {
        /// <summary>
        /// Creates a new <see cref="AppHostBase"/> instance.
        /// </summary>
        public AppHostBase(IReadOnlyList<string> args) : base(args) { }

        /// <summary>
        /// Creates a new <see cref="RpcServiceBuilder"/> instance.
        /// </summary>
        /// <remarks>Override this method if you want to use your own <see cref="Rpc.RpcService"/> and/or <see cref="RpcServiceBuilder"/> implementation.</remarks>
        protected virtual RpcServiceBuilder CreateServiceBuilder() => new();

        /// <summary>
        /// Returns the underlying <see cref="Rpc.RpcService"/>.
        /// </summary>
        public RpcService? RpcService { get; private set; }

        /// <summary>
        /// Invoked on service setup.
        /// </summary>
        public virtual void OnConfigure(RpcServiceBuilder serviceBuilder)
        {
            if (serviceBuilder is null)
                throw new ArgumentNullException(nameof(serviceBuilder));

            serviceBuilder.ConfigureModules(registry => registry.Register<IServiceDescriptor, ServiceDescriptor>());
        }

        /// <summary>
        /// Called on host start
        /// </summary>
        public virtual void OnStart()
        {
            if (RpcService is null)
            {
                RpcServiceBuilder serviceBuilder = CreateServiceBuilder();
                OnConfigure(serviceBuilder);
                RpcService = serviceBuilder.Build();
            }

            RpcService.Start();
        }

        /// <summary>
        /// Called on host termination
        /// </summary>
        public virtual void OnStop() => RpcService?.Stop();
    }
}
