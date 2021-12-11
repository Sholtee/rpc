/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.ServiceProcess;

namespace Solti.Utils.Rpc.Hosting
{
    using Rpc.Internals;

    public partial class AppHostBase
    {
        private sealed class ServiceImpl : ServiceBase
        {
            public AppHostBase Host { get; }

            public ServiceImpl(AppHostBase owner) : base() => Host = owner;

            protected override void OnStart(string[] args)
            {
                base.OnStart(args);
                Host.OnStart();
            }

            protected override void OnStop()
            {
                base.OnStop();
                Host.OnStop();
            }
        }

        [Verb("service")]
        internal void OnStartWin32Service()
        {
            using ServiceImpl impl = new(this);

            //
            // Blokkolodik
            //

            ServiceBase.Run(impl);
        }
    }
}
