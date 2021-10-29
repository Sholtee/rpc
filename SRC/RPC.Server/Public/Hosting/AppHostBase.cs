/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Solti.Utils.Rpc.Hosting
{
    using Interfaces;

    using Primitives.Patterns;
    using Rpc.Internals;

    /// <summary>
    /// Represents the base class of the host application.
    /// </summary>
    public abstract class AppHostBase: Disposable, IHost
    {
        private readonly RpcServiceBuilder FServiceBuilder = new RpcServiceBuilder()
            .ConfigureModules(registry => registry.Register<IServiceDescriptor, ServiceDescriptor>());

        /// <inheritdoc/>
        protected override void Dispose(bool disposeManaged)
        {
            RpcService?.Stop();

            if (disposeManaged)
                RpcService?.Dispose();

            base.Dispose(disposeManaged);
        }

        /// <summary>
        /// Returns the underlying <see cref="Rpc.RpcService"/>.
        /// </summary>
        public RpcService? RpcService { get; private set; }

        /// <summary>
        /// Returns the name of the host.
        /// </summary>
        public string Name { get; protected init; } = "MyApp";

        /// <summary>
        /// Gets or sets description of the host.
        /// </summary>
        public string? Description { get; protected init; }

        /// <summary>
        /// Indicates if the host should be started automatically.
        /// </summary>
        public bool AutoStart { get; protected init; }

        IEnumerable<string> IHost.Dependencies => Dependencies;

        /// <summary>
        /// Service dependencies that must run.
        /// </summary>
        protected ICollection<string> Dependencies { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? (ICollection<string>) new List<string>()
            : (ICollection<string>) Array.Empty<string>();

        /// <summary>
        /// Invoked on service setup.
        /// </summary>
        public virtual void OnBuildService(RpcServiceBuilder serviceBuilder) { }

        /// <summary>
        /// Invoked on service installation.
        /// </summary> 
        public virtual void OnInstall() {}

        /// <summary>
        /// Invoked on service removal.
        /// </summary>
        public virtual void OnUninstall() {}

        /// <summary>
        /// Invoked on service startup.
        /// </summary>
        public virtual void OnStart(HostConfiguration configuration)
        {
            if (RpcService is null)
            {
                RpcServiceBuilder serviceBuilder = new();
                OnBuildService(serviceBuilder);
                RpcService = serviceBuilder.Build();
            }

            RpcService.Start();
        }

        /// <summary>
        /// Invoked on service termination.
        /// </summary>
        public virtual void OnStop() => RpcService?.Stop();

        /// <summary>
        /// Invoked on unhandled exception.
        /// </summary>
        public virtual void OnUnhandledException(Exception ex) {}
    }
}
