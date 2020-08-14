/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Hosting
{
    using DI;
    using DI.Interfaces;

    using Rpc.Interfaces;
    using Rpc.Internals;

    using Primitives.Patterns;
    using Properties;

    /// <summary>
    /// Represents the an app host that can be invoked by RPC
    /// </summary>
    public abstract class AppHostBase: Disposable, IHost
    {
        private readonly IServiceContainer FContainer;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AppHostBase(IServiceContainer container, RpcService rpcService)
        {
            FContainer = container ?? throw new ArgumentNullException(nameof(container));
            RpcService = rpcService ?? throw new ArgumentNullException(nameof(rpcService));           
            Runner     = HostRunner.GetCompatibleRunner(this);
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AppHostBase(IServiceContainer container) : this(container, new RpcService(container ?? throw new ArgumentNullException(nameof(container)))) {}

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AppHostBase(): this(new ServiceContainer()) {}

        /// <summary>
        /// The name of the host.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The URL on which the RPC service will listen.
        /// </summary>
        [SuppressMessage("Design", "CA1056:Uri properties should not be strings")]
        public abstract string Url { get; }

        /// <summary>
        /// The related <see cref="IHostRunner"/>.
        /// </summary>
        public IHostRunner Runner { get; }

        /// <summary>
        /// The related <see cref="Rpc.RpcService"/>.
        /// </summary>
        public RpcService RpcService { get; }

        /// <summary>
        /// Indicates whether this host was initialized or not.
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// The description of the host.
        /// </summary>
        public string? Description { get; protected set; }

        /// <summary>
        /// Indicates if the host should be started automatically.
        /// </summary>
        public bool AutoStart { get; protected set; }

        /// <inheritdoc/>
        public ILogger? Logger { get; protected set; } = TraceLogger.Create<AppHostBase>();

        IEnumerable<string> IHost.Dependencies => Dependencies;

        /// <summary>
        /// Services that must run.
        /// </summary>
        protected ICollection<string> Dependencies { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? (ICollection<string>) new List<string>()
            : (ICollection<string>) Array.Empty<string>();

        /// <summary>
        /// Invoked on service installation.
        /// </summary> 
        public virtual void OnInstall() => Logger?.LogInformation(Trace.INSTALLING_HOST);

        /// <summary>
        /// Invoked on service removal.
        /// </summary>
        public virtual void OnUninstall() => Logger?.LogInformation(Trace.UNINSTALLING_HOST);

        /// <summary>
        /// Place of module registration routines.
        /// </summary>
        public virtual void OnRegisterModules(IModuleRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            registry.Register<IServiceDescriptor, ServiceDescriptor>();
        }

        /// <summary>
        /// Place of service registration routines.
        /// </summary>
        public virtual void OnRegisterServices(IServiceContainer container) 
        {
            container
                .Instance<IReadOnlyList<string>>("CommandLineArgs", Environment.GetCommandLineArgs())
                .Instance("EnvironmentVariables", GetEnvironmentVariables())
                .Instance(Runner);

            IReadOnlyDictionary<object, object> GetEnvironmentVariables() 
            {
                IDictionary variables = Environment.GetEnvironmentVariables();
                return variables
                    .Keys
                    .Cast<object>()
                    .ToDictionary(key => key, key => variables[key]);
            }
        }

        /// <summary>
        /// Invoked on service startup.
        /// </summary>
        public virtual void OnStart()
        {
            Logger?.LogInformation(Trace.STARTING_HOST);

            try
            {
                if (!Initialized)
                {
                    OnRegisterServices(FContainer);
                    OnRegisterModules(RpcService);
                    Initialized = true;
                }

                RpcService.Start(Url);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, Trace.STARTING_HOST_FAILED);
                throw;
            }
        }

        /// <summary>
        /// Invoked on service termination.
        /// </summary>
        public virtual void OnStop()
        {
            Logger?.LogInformation(Trace.TERMINATING_HOST);

            RpcService.Stop();
        }

        /// <summary>
        /// Invoked on unhandled exception.
        /// </summary>
        public virtual void OnUnhandledException(Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            Logger?.LogError(ex, Trace.UNHANDLED_EXCEPTION);
        }

        /// <summary>
        /// See <see cref="IDisposable.Dispose"/>.
        /// </summary>
        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
            {
                Runner.Dispose();
                RpcService.Dispose();
                FContainer.Dispose();
            }
            base.Dispose(disposeManaged);
        }
    }
}
