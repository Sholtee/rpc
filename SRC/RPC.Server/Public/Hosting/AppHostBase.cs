/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Solti.Utils.Rpc
{
    using Interfaces;

    using Primitives.Patterns;
    using Rpc.Internals;

    /// <summary>
    /// Represents the an app host that can be invoked through RPC.
    /// </summary>
    public abstract class AppHostBase: Disposable, IHost
    {
        /// <summary>
        /// The host builder.
        /// </summary>
        protected RpcServiceBuilder ServiceBuilder { get; } = new RpcServiceBuilder().ConfigureModules(registry => registry.Register<IServiceDescriptor, ServiceDescriptor>());

        /// <inheritdoc/>
        protected override void Dispose(bool disposeManaged)
        {
            RpcService?.Stop();

            if (disposeManaged)
                RpcService?.Dispose();

            base.Dispose(disposeManaged);
        }

        /// <summary>
        /// The name of the host.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The related <see cref="Rpc.RpcService"/>.
        /// </summary>
        public RpcService? RpcService { get; private set; }

        /// <summary>
        /// The description of the host.
        /// </summary>
        public string? Description { get; protected set; }

        /// <summary>
        /// Indicates if the host should be started automatically.
        /// </summary>
        public bool AutoStart { get; protected set; }

        IEnumerable<string> IHost.Dependencies => Dependencies;

        /// <summary>
        /// Service dependencies that must run.
        /// </summary>
        protected ICollection<string> Dependencies { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? (ICollection<string>) new List<string>()
            : (ICollection<string>) Array.Empty<string>();

        /// <summary>
        /// Invoked on service installation.
        /// </summary> 
        public virtual void OnInstall()
        {
            //Logger?.LogInformation(Trace.INSTALLING_HOST);
        }

        /// <summary>
        /// Invoked on service removal.
        /// </summary>
        public virtual void OnUninstall()
        {
            //Logger?.LogInformation(Trace.UNINSTALLING_HOST);
        }

        /// <summary>
        /// Invoked on service startup.
        /// </summary>
        public virtual void OnStart(HostConfiguration configuration)
        {
            //Logger?.LogInformation(Trace.STARTING_HOST);

            try
            {
                RpcService ??= ServiceBuilder.Build();
                RpcService.Start();
            }
            catch (Exception)
            {
                //Logger?.LogError(ex, Trace.STARTING_HOST_FAILED);
                throw;
            }
        }

        /// <summary>
        /// Invoked on service termination.
        /// </summary>
        public virtual void OnStop()
        {
            //Logger?.LogInformation(Trace.TERMINATING_HOST);

            RpcService?.Stop();
        }

        /// <summary>
        /// Invoked on unhandled exception.
        /// </summary>
        public virtual void OnUnhandledException(Exception ex)
        {
            if (ex is null)
                throw new ArgumentNullException(nameof(ex));

            //Logger?.LogError(ex, Trace.UNHANDLED_EXCEPTION);
        }
    }
}
