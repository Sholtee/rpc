/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace Solti.Utils.Rpc.Hosting
{
    using DI;
    using DI.Interfaces;
    using Primitives.Patterns;

    /// <summary>
    /// Represents the an app host that can be invoked by RPC
    /// </summary>
    public abstract class AppHostBase: Disposable, IHost
    {
        private readonly IServiceContainer FContainer;
        private readonly RpcService FRpcService;
        private readonly string FTraceCategory;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AppHostBase(IServiceContainer container, RpcService rpcService)
        {     
            FContainer     = container ?? throw new ArgumentNullException(nameof(container));
            FRpcService    = rpcService ?? throw new ArgumentNullException(nameof(rpcService));           
            FTraceCategory = $"[{GetType().Name}]";
            Runner         = HostRunner.GetFor(this);
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AppHostBase(IServiceContainer container) : this(container, new RpcService(container ?? throw new ArgumentNullException(nameof(container))))
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AppHostBase(): this(new ServiceContainer())
        {
        }

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
        public virtual void OnInstall() => Trace.WriteLine(nameof(OnInstall), FTraceCategory);

        /// <summary>
        /// Invoked on service removal.
        /// </summary>
        public virtual void OnUninstall() => Trace.WriteLine(nameof(OnUninstall), FTraceCategory);

        /// <summary>
        /// Place of module registration routines.
        /// </summary>
        public virtual void OnRegisterModules(IModuleRegistry registry) { }

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
            Trace.WriteLine(nameof(OnStart), FTraceCategory);

            if (!Initialized)
            {
                OnRegisterServices(FContainer);
                OnRegisterModules(FRpcService);
                Initialized = true;
            }
            FRpcService.Start(Url);
        }

        /// <summary>
        /// Invoked on service termination.
        /// </summary>
        public virtual void OnStop()
        {
            Trace.WriteLine(nameof(OnStop), FTraceCategory);

            FRpcService.Stop();
        }

        /// <summary>
        /// Invoked on unhandled exception.
        /// </summary>
        public virtual void OnUnhandledException(Exception ex)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            Trace.WriteLine($"Unhandled exception: {ex.Message}", FTraceCategory);
        }

        /// <summary>
        /// See <see cref="IDisposable.Dispose"/>.
        /// </summary>
        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
            {
                Runner.Dispose();
                FRpcService.Dispose();
                FContainer.Dispose();
            }
            base.Dispose(disposeManaged);
        }
    }
}
