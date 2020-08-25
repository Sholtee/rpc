/********************************************************************************
* ServiceHostRunner_WinNT.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    using Interfaces;

    internal class ServiceHostRunner_WinNT : HostRunner
    {
        private sealed class ServiceImpl : ServiceBase
        {
            public IHostRunner Parent { get; }

            public ServiceImpl(IHostRunner owner) : base()
            {               
                Parent = owner;
                ServiceName = Parent.Host.Name;
            }

            protected override void OnStart(string[] args)
            {
                base.OnStart(args);
                Parent.Host.OnStart(Parent.Configuration);
            }

            protected override void OnStop()
            {
                base.OnStop();
                Parent.Host.OnStop();
            }
        }

        private readonly ServiceImpl FServiceImpl;

        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged) FServiceImpl.Dispose();
            base.Dispose(disposeManaged);
        }

        internal ServiceHostRunner_WinNT(IHost host, HostConfiguration configuration) : base(host, configuration) => FServiceImpl = new ServiceImpl(this);

        public override void Start() =>
            //
            // Blokkolodik
            //

            ServiceBase.Run(FServiceImpl);

        public override void Stop() => FServiceImpl.Stop();

        #region Factory
        private sealed class FactoryImpl : IHostRunnerFactory
        {
            public bool IsCompatible(IHost host) => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && ProcessExtensions.IsService;

            public IHostRunner CreateRunner(IHost host, HostConfiguration configuration) => new ServiceHostRunner_WinNT(host, configuration);
        }

        public static IHostRunnerFactory Factory { get; } = new FactoryImpl();
        #endregion
    }
}
