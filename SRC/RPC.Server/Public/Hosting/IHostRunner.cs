/********************************************************************************
* IHostRunner.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc.Hosting
{
    /// <summary>
    /// Defines an abstract host runner.
    /// </summary>
    public interface IHostRunner: IDisposable
    {
        /// <summary>
        /// The host related to this runner.
        /// </summary>
        IHost Host { get; }

        /// <summary>
        /// Starts the host.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the host.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        void Stop();
    }
}
