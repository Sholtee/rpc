/********************************************************************************
* ConsoleHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    using Interfaces;
    using Properties;

    internal class ConsoleHostRunner : HostRunner
    {
        private readonly ManualResetEventSlim FTerminate = new ManualResetEventSlim();

        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged) FTerminate.Dispose();
            base.Dispose(disposeManaged);
        }

        internal ConsoleHostRunner(IHost host, HostConfiguration configuration) : base(host, configuration) { }

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        public override void Start()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                List<string> missingDependencies = new();

                IReadOnlyList<ServiceController>
                    services = ServiceController.GetServices(),
                    dependencies = Host
                        .Dependencies
                        .Select(dep => 
                        {
                            ServiceController? instance = services.SingleOrDefault(svc => svc.ServiceName == dep);
                            if (instance is null)
                                missingDependencies.Add(dep);
                            return instance!;
                        })
                        .ToArray();

                if (missingDependencies.Any())
                    #pragma warning disable CA2201 // To preserve backward compatibility keep throwing simple Exception only
                    throw new Exception(string.Format(Errors.Culture, Errors.DEPENDENCY_NOT_AVAILABLE, string.Join(",", missingDependencies)));
                    #pragma warning restore CA2201

                bool depsAvailable = Task.WaitAll
                (
                    dependencies
                        .Where(svc => svc.Status is not ServiceControllerStatus.Running)
                        .Select(svc => Task.Run(() => svc.WaitForStatus(ServiceControllerStatus.Running)))
                        .ToArray(),
                    TimeSpan.FromSeconds(10) // TODO: ezt konfiguralhatova tenni
                );
                if (!depsAvailable)
                    #pragma warning disable CA2201 // Like above...
                    throw new Exception(Errors.DEPENDENCY_NOT_RUNNING);
                    #pragma warning restore CA2201
            }

            Console.Title = Host.Name;
            Console.CancelKeyPress += OnConsoleCancel;

            try
            {
                Host.OnStart(Configuration);

                Task.Factory.StartNew(() =>
                {
                    Console.WriteLine(Trace.RUNNING);
                    while (true) Console.ReadKey(true);
                }, TaskCreationOptions.LongRunning);

                FTerminate.Wait();
            }
            finally 
            {
                Host.OnStop();
            }
        }

        internal void OnConsoleCancel(object sender, ConsoleCancelEventArgs e) 
        {
            Stop();

            //
            // E nelkul parhuzamosan ket modon probalnank leallitani az app-ot
            //
#if DEBUG
            if (e != null)
#endif
                e.Cancel = true;
        }

        public override void Stop() => FTerminate.Set();

        #region Factory
        private sealed class FactoryImpl : IHostRunnerFactory
        {
            public bool IsCompatible(IHost host) => HostRunner.IsInteractive;

            public IHostRunner CreateRunner(IHost host, HostConfiguration configuration) => new ConsoleHostRunner(host, configuration);
        }

        public static IHostRunnerFactory Factory { get; } = new FactoryImpl();
        #endregion
    }
}
