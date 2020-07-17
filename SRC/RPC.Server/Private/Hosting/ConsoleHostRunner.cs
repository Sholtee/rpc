/********************************************************************************
* ConsoleHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    using Properties;

    internal class ConsoleHostRunner : HostRunner
    {
        private readonly ManualResetEventSlim FTerminate = new ManualResetEventSlim();

        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged) FTerminate.Dispose();
            base.Dispose(disposeManaged);
        }

        public ConsoleHostRunner(IHost host) : base(host) { }

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        public override void Start()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ServiceController[] services = ServiceController.GetServices();

                string[] missingServices = Host
                    .Dependencies
                    .Where(dep => services.SingleOrDefault(svc => svc.ServiceName == dep)?.Status != ServiceControllerStatus.Running)
                    .ToArray();

                if (missingServices.Any())
                    throw new Exception(string.Format(Resources.Culture, Resources.DEPENDENCY_NOT_AVAILABLE, string.Join(",", missingServices)));
            }

            Console.Title = Host.Name;
            Console.CancelKeyPress += OnConsoleCancel;

            try
            {
                Host.OnStart();

                Task.Factory.StartNew(() =>
                {
                    Console.WriteLine(Resources.RUNNING);
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

        public override bool ShouldUse => Environment.UserInteractive;

        public override void Stop() => FTerminate.Set();
    }
}
