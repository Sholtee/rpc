/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AppHostBase(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

        /// <summary>
        /// The name of the host.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The description of the host.
        /// </summary>
        public string? Description { get; protected set; }

        /// <summary>
        /// Indicates if the host should be started automatically.
        /// </summary>
        public bool AutoStart { get; protected set; }

        /// <summary>
        /// Services that must run.
        /// </summary>
        public ICollection<string> Dependencies { get; } = new HashSet<string>();

        /// <summary>
        /// Invoked on service installation.
        /// </summary>
        public virtual void OnInstall() { }

        /// <summary>
        /// Invoked on service removal.
        /// </summary>
        public virtual void OnUninstall() { }

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
                .Instance("EnvironmentVariables", Environment.GetEnvironmentVariables()); // TODO: to readonly
        }

        /// <summary>
        /// Invoked on service startup.
        /// </summary>
        public virtual void OnStart() { }

        /// <summary>
        /// Invoked on service termination.
        /// </summary>
        public virtual void OnStop() { }
    }
}
