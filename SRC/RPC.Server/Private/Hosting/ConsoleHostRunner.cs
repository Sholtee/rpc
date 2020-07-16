/********************************************************************************
* ConsoleHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

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

        protected override void Start()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ServiceController[] services = ServiceController.GetServices();

                string[] missingServices = Host
                    .Dependencies
                    .Where(dep => services.SingleOrDefault(svc => svc.ServiceName == dep)?.Status != ServiceControllerStatus.Running)
                    .ToArray();

                if (missingServices.Any())
                {
                    Console.Error.WriteLine(string.Format(Resources.Culture, Resources.DEPENDENCY_NOT_AVAILABLE, missingServices));
                    Environment.Exit(-1);
                }
            }

            Console.CancelKeyPress += (s, e) => Stop();

            try
            {
                Host.OnStart();

                Console.WriteLine(Resources.RUNNING);

                FTerminate.Wait();
            }
            finally 
            {
                Host.OnStop();
            }
        }

        public override bool ShouldUse => Environment.UserInteractive;

        protected override void Stop() => FTerminate.Set();
    }
}
