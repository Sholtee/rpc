/********************************************************************************
* ServiceHostRunner_WinNT.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    internal class ServiceHostRunner_WinNT : HostRunner
    {
        private sealed class ServiceImpl : ServiceBase 
        {
            public AppHostBase Owner { get; }

            public ServiceImpl(AppHostBase owner) : base()
            {
                ServiceName = owner.Name;
                Owner = owner;
            }

            protected override void OnStart(string[] args)
            {
                Owner.OnStart();
                base.OnStart(args);
            }

            protected override void OnStop()
            {
                Owner.OnStop();
                base.OnStop();
            }
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        public override void Run(AppHostBase appHost) => ServiceBase.Run(new ServiceImpl(appHost));

        public override bool ShouldUse() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Environment.UserInteractive;
    }
}
