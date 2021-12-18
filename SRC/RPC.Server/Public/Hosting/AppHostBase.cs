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
        /// <remarks>This property is set only once, after successful service configuration.</remarks>
        public RpcService? RpcService { get; private set; }

        /// <summary>
        /// Invoked once on service setup.
        /// </summary>
        /// <remarks>Override this method to configure the RPC service being built.</remarks>
        public virtual void OnConfigure(RpcServiceBuilder serviceBuilder)
        {
            if (serviceBuilder is null)
                throw new ArgumentNullException(nameof(serviceBuilder));

            serviceBuilder.ConfigureModules(registry => registry.Register<IServiceDescriptor, ServiceDescriptor>());
        }

        /// <summary>
        /// Called once when the <see cref="RpcService"/> is built successfully.
        /// </summary>
        /// <remarks>Override this method if you need to implement one-time initialization logic that depends on the built service.</remarks>
        public virtual void OnBuilt() {}

        /// <summary>
        /// Called on host start
        /// </summary>
        /// <remarks>In most of cases you should not override this method. To configure your service instance use the <see cref="OnConfigure(RpcServiceBuilder)"/> and <see cref="OnBuilt"/> methods.</remarks>
        public virtual void OnStart()
        {
            if (RpcService is null)
            {
                RpcServiceBuilder serviceBuilder = CreateServiceBuilder();
                OnConfigure(serviceBuilder);
                RpcService = serviceBuilder.Build();
                OnBuilt();
            }

            RpcService.Start();
        }

        /// <summary>
        /// Called on host termination
        /// </summary>
        /// <remarks>Override this method if you need to do some resource cleanup.</remarks>
        public virtual void OnStop() => RpcService?.Stop();
    }
}
