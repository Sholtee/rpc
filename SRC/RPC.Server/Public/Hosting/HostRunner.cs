/********************************************************************************
* HostRunner.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc.Hosting
{
    using Primitives.Patterns;

    /// <summary>
    /// Defines an abstract host runner.
    /// </summary>
    public abstract class HostRunner: Disposable, IHostRunner
    {
        /// <summary>
        /// The host related to this runner.
        /// </summary>
        public IHost Host { get; }

        /// <summary>
        /// Creates a new <see cref="HostRunner"/> instance.
        /// </summary>
        protected HostRunner(IHost host) => Host = host ?? throw new ArgumentNullException(nameof(host));

        /// <summary>
        /// If overridden in the derived class it should determines whether the runner should be used.
        /// </summary>
        public abstract bool ShouldUse();

        /// <summary>
        /// Starts the host.
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stops the host.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        public abstract void Stop();
    }
}
