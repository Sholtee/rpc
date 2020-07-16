/********************************************************************************
* IHost.cs                                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Solti.Utils.Rpc.Hosting
{
    /// <summary>
    /// Represents an abstract service host.
    /// </summary>
    public interface IHost
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
        ICollection<string> Dependencies { get; }

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
        void OnStart();

        /// <summary>
        /// Invoked on service termination.
        /// </summary>
        void OnStop();
    }
}
