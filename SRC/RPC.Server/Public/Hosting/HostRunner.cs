/********************************************************************************
* HostRunner.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Solti.Utils.Rpc.Hosting
{
    using Internals;
    using Primitives;
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
        public abstract bool ShouldUse { get; }

        /// <summary>
        /// See <see cref="IHostRunner.IsStarted"/>.
        /// </summary>
        public bool IsStarted { get; private set; }

        void IHostRunner.Start() 
        {
            if (IsStarted) throw new InvalidOperationException();
            Start();
            IsStarted = true;
        }

        void IHostRunner.Stop() 
        {
            if (!IsStarted) throw new InvalidOperationException();
            Stop();
            IsStarted = false;
        }

        /// <summary>
        /// Starts the host.
        /// </summary>
        protected abstract void Start();

        /// <summary>
        /// Stops the host.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        protected abstract void Stop();

        //
        // A legutoljara regisztralt futtatot vizsgaljuk eloszor kompatibilitasi szempontbol.
        //

        private static Stack<Func<object[], object>> RunnerCtors { get; } = new Stack<Func<object[], object>>();

        /// <summary>
        /// Extends the set of available host runners.
        /// </summary>
        public static void Extend<THostRunner>() where THostRunner : HostRunner 
        {
            Func<object[], object>? ctor = typeof(THostRunner)
                .GetConstructor(new[] { typeof(IHost) })
                ?.ToStaticDelegate();

            if (ctor != null) RunnerCtors.Push(ctor);
        }

        static HostRunner() 
        {
            Extend<DefaultHostRunner>();
            Extend<ConsoleHostRunner>();
            Extend<InstallHostRunner_WinNT>();
            Extend<ServiceHostRunner_WinNT>();
        }

        /// <summary>
        /// Gets the compatible runner for the given <paramref name="host"/>.
        /// </summary>
        public static IHostRunner GetFor(IHost host) 
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            return RunnerCtors
                .Select(ctor => (HostRunner) ctor.Invoke(new object[] { host }))
                .Where(runner =>
                {
                    bool shouldUse = runner.ShouldUse;
                    if (!shouldUse) 
                        runner.Dispose();

                    return shouldUse;
                })
                .First();
        }
    }
}
