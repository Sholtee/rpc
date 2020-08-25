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
        /// The configuration in which the host should run.
        /// </summary>
        public HostConfiguration Configuration { get; }

        /// <summary>
        /// Creates a new <see cref="HostRunner"/> instance.
        /// </summary>
        protected HostRunner(IHost host, HostConfiguration configuration)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Configuration = configuration;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionEventHandler;

            host.Logger?.LogInformation(string.Format(Trace.Culture, Trace.STARTING_HOST_WITH, GetType().Name, configuration));
        }

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
        /// Creates a compatible runner for the given <paramref name="host"/>.
        /// </summary>
        public static IHostRunner CreateRunner(IHost host, HostConfiguration configuration) 
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            foreach (IHostRunnerFactory factory in RunnerFactories)
            {
                if (factory.IsCompatible(host))
                {
                    return factory.CreateRunner(host, configuration);
                }
            }

            throw new NotSupportedException(); // elvileg ide sose jutunk el
        }

        /// <summary>
        /// Runs the given host.
        /// </summary>
        [SuppressMessage("Performance", "CA1806:Do not ignore method results")]
        public static void Run<THost>() where THost : IHost, new()
        {
            using IHost host = new THost();

            HostConfiguration config = HostConfiguration.Debug;

            //
            // Konfiguracio kiolvasasa az [appname].runtimeconfig.json fajlbol
            //

            object? data = AppContext.GetData("hostConfiguration");

            if (data != null) 
                Enum.TryParse(data.ToString(), out config);

            using IHostRunner runner = CreateRunner(host, config);

            //
            // Blokkolodik
            //

            runner.Start();
        }
    }
}
