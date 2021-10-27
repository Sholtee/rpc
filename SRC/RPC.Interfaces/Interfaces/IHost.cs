/********************************************************************************
* IHost.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Represents an abstract service host.
    /// </summary>
    public interface IHost: IDisposable
    {
        /// <summary>
        /// The name of the host.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The description of the host.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Indicates if the host should be started automatically.
        /// </summary>
        bool AutoStart { get; }

        /// <summary>
        /// Services that must run.
        /// </summary>
        IEnumerable<string> Dependencies { get; }

        /// <summary>
        /// Invoked on host installation.
        /// </summary>
        void OnInstall();

        /// <summary>
        /// Invoked on host removal.
        /// </summary>
        void OnUninstall();

        /// <summary>
        /// Invoked on host startup.
        /// </summary>
        void OnStart(HostConfiguration configuration);

        /// <summary>
        /// Invoked on host termination.
        /// </summary>
        void OnStop();

        /// <summary>
        /// Invoked on unhandled exception.
        /// </summary>
        void OnUnhandledException(Exception ex);
    }
}
