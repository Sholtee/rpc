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
        public override void Run(AppHostBase appHost)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ServiceController[] services = ServiceController.GetServices();

                string[] missingServices = appHost
                    .Dependencies
                    .Where(dep => services.SingleOrDefault(svc => svc.ServiceName == dep)?.Status != ServiceControllerStatus.Running)
                    .ToArray();

                if (missingServices.Any())
                {
                    Console.Error.WriteLine(string.Format(Resources.Culture, Resources.DEPENDENCY_NOT_AVAILABLE, missingServices));
                    Environment.Exit(-1);
                }
            }

            using var terminateEvt = new ManualResetEventSlim();

            Console.CancelKeyPress += (s, e) => terminateEvt.Set();

            try
            {
                appHost.OnStart();

                Console.WriteLine(Resources.RUNNING);

                terminateEvt.Wait();
            }
            finally 
            {
                appHost.OnStop();
            }
        }

        public override bool ShouldUse() => Environment.UserInteractive;
    }
}
