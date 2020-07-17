/********************************************************************************
* ServiceHostRunner_WinNT.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    internal class ServiceHostRunner_WinNT : HostRunner
    {
        private sealed class ServiceImpl : ServiceBase 
        {
            public IHost Owner { get; }

            public ServiceImpl(IHost owner) : base()
            {
                ServiceName = owner.Name;
                Owner = owner;
            }

            protected override void OnStart(string[] args)
            {
                base.OnStart(args);
                Owner.OnStart();
                
            }

            protected override void OnStop()
            {
                base.OnStop();
                Owner.OnStop();
            }
        }

        private readonly ServiceImpl? FServiceImpl;

        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged) FServiceImpl?.Dispose();
            base.Dispose(disposeManaged);
        }

        public ServiceHostRunner_WinNT(IHost host) : base(host)
        {
            if (ShouldUse)
                //
                // Ez dobhat PlatformNotSupportedException-t nem Win alatt -> ShouldUse
                //

                FServiceImpl = new ServiceImpl(host);
        }

        protected override void Start() => 
            //
            // Blokkolodik
            //

            ServiceBase.Run(FServiceImpl ?? throw new PlatformNotSupportedException());

        protected override void Stop()
        {
            if (FServiceImpl == null) throw new PlatformNotSupportedException();
            FServiceImpl.Stop();
        }

        public override bool ShouldUse => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Environment.UserInteractive;
    }
}
