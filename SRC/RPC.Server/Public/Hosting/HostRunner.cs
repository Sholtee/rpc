/********************************************************************************
* HostRunner.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

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
        private void UnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e) => Host.OnUnhandledException((Exception) e.ExceptionObject);

        /// <summary>
        /// See <see cref="IDisposable"/>.
        /// </summary>
        protected override void Dispose(bool disposeManaged)
        {
            AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionEventHandler;
            base.Dispose(disposeManaged);
        }

        /// <summary>
        /// The host related to this runner.
        /// </summary>
        public IHost Host { get; }

        /// <summary>
        /// Creates a new <see cref="HostRunner"/> instance.
        /// </summary>
        protected HostRunner(IHost host)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionEventHandler;
        }

        /// <summary>
        /// If overridden in the derived class it should determines whether the runner should be used.
        /// </summary>
        public abstract bool ShouldUse { get; }

        /// <summary>
        /// Starts the host.
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stops the host.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        public abstract void Stop();

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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Extend<InstallHostRunner_WinNT>();
                Extend<ServiceHostRunner_WinNT>();
            }
        }

        /// <summary>
        /// Gets the compatible runner for the given <paramref name="host"/>.
        /// </summary>
        public static IHostRunner GetFor(IHost host) 
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            foreach (Func<object[], object> ctor in RunnerCtors)
            {
                var runner = (HostRunner) ctor.Invoke(new object[] { host });
                if (runner.ShouldUse)
                {
                    Trace.WriteLine($"Running host with {runner.GetType().Name}", $"[{nameof(HostRunner)}]");
                    return runner;
                }
                runner.Dispose();
            }

            throw new NotSupportedException(); // elvileg ide sose jutunk el
        }
    }
}
