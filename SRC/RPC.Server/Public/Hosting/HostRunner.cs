/********************************************************************************
* HostRunner.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Hosting
{
    using Interfaces;
    using Internals;
    using Primitives.Patterns;
    using Properties;

    /// <summary>
    /// Defines an abstract host runner.
    /// </summary>
    public abstract class HostRunner : Disposable, IHostRunner
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

            host.Logger?.LogInformation(string.Format(Trace.Culture, Trace.STARTING_HOST_WITH, GetType().Name, Configuration));
        }

        /// <summary>
        /// The configuration in which the host should run.
        /// </summary>
        public static HostConfiguration Configuration { get; }

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
        // A legutoljara regisztralt gyarat vizsgaljuk eloszor kompatibilitasi szempontbol.
        //

        private static Stack<IHostRunnerFactory> RunnerFactories { get; } = new Stack<IHostRunnerFactory>();

        /// <summary>
        /// Extends the set of the available <see cref="IHostRunnerFactory"/>s.
        /// </summary>
        public static void RegisterFactory(IHostRunnerFactory factory) => RunnerFactories.Push(factory);

        static HostRunner() 
        {
            //
            // Konfiguracio kiolvasasa az [appname].runtimeconfig.json fajlbol
            //

            object? config = AppContext.GetData("hostConfiguration");
            if (config != null)
                Configuration = (HostConfiguration) Enum.Parse(typeof(HostConfiguration), (string) config);

            //
            // Alapertelmezett host futtatok regisztralasa
            //

            RegisterFactory(DefaultHostRunner.Factory);
            RegisterFactory(ConsoleHostRunner.Factory);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RegisterFactory(InstallHostRunner_WinNT.Factory);
                RegisterFactory(ServiceHostRunner_WinNT.Factory);
            }
        }

        /// <summary>
        /// Gets the compatible runner for the given <paramref name="host"/>.
        /// </summary>
        public static IHostRunner GetCompatibleRunner(IHost host) 
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            foreach (IHostRunnerFactory factory in RunnerFactories)
            {
                if (factory.ShouldUse)
                {
                    return factory.CreateRunner(host);
                }
            }

            throw new NotSupportedException(); // elvileg ide sose jutunk el
        }

        /// <summary>
        /// Runs the given host.
        /// </summary>
        public static void Run<THost>() where THost : IHost, new()
        {
            using IHost host = new THost();
            using IHostRunner runner = GetCompatibleRunner(host);
                runner.Start();
        }
    }
}
