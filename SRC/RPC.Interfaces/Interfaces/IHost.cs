/********************************************************************************
* IHost.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

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
        /// The <see cref="ILogger"/> related to the host.
        /// </summary>       
        #pragma warning disable CS3003 // ILogger is not CLS-compliant
        ILogger? Logger { get; }
        #pragma warning restore CS3003

        /// <summary>
        /// Invoked on service installation.
        /// </summary>
        void OnInstall();

        /// <summary>
        /// Invoked on service removal.
        /// </summary>
        void OnUninstall();

        /// <summary>
        /// Invoked on service startup.
        /// </summary>
        void OnStart(HostConfiguration configuration);

        /// <summary>
        /// Invoked on service termination.
        /// </summary>
        void OnStop();

        /// <summary>
        /// Invoked on unhandled exception.
        /// </summary>
        void OnUnhandledException(Exception ex);
    }
}
