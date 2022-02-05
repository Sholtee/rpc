/********************************************************************************
* AppHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Server.Sample
{
    using DI.Interfaces;
    using Hosting;
    using Interfaces;
    using Pipeline;
    using Servers;

    public class AppHost : AppHostBase
    {
        public AppHost(string[] args) : base(args) { }

        public override void OnConfigureWin32Service(Win32ServiceDescriptor descriptor)
        {
            base.OnConfigureWin32Service(descriptor);

            descriptor.Name = "Calculator";
        }

        public override void OnConfigure(WebServiceBuilder serviceBuilder) => serviceBuilder
            .ConfigureBackend(_ => new HttpListenerBackend("http://localhost:1986/api/") { ReserveUrl = true })
            .ConfigureRpcService(conf => 
            {
                switch (conf) 
                {
                    case Modules modules:
                        modules.Register<ICalculator, Calculator>();
                        break;
                    case HttpAccessControl hac:
                        hac.AllowedOrigins.Add("http://localhost:1987");
                        break;
                }
            }, useDefaultLogger: false)
            .ConfigureServices(services => 
            {
                //
                // A naplozas kikapcsolhato mivel az a teljesitmeny teszteket negativan befolyasolja.
                //

                if (!Args.Any(arg => arg.ToLowerInvariant() is "-nolog"))
                    services.Factory<ILogger>(i => ConsoleLogger.Create<AppHost>(), Lifetime.Scoped);
            });
    }
}
