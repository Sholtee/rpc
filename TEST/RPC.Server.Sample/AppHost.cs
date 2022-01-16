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

    public class AppHost : AppHostBase
    {
        public AppHost(string[] args) : base(args) { }

        public override void OnConfigureWin32Service(Win32ServiceDescriptor descriptor)
        {
            base.OnConfigureWin32Service(descriptor);

            descriptor.Name = "Calculator";
        }

        public override void OnConfigure(WebServiceBuilder serviceBuilder)
        {
            serviceBuilder.Url = "http://localhost:1986/api/";
            serviceBuilder
                .DefineRpcService(conf => 
                {
                    switch (conf) 
                    {
                        case Modules modules:
                            modules.Register<ICalculator, Calculator>();
                            break;
                        case RpcAccessControl ac:
                            ac.AllowedMethods.Add("http://localhost:1987");
                            break;
                    }
                })
                .ConfigureServices(services => 
                {
                    //
                    // A naplozas kikapcsolhato mivel az a teljesitmeny teszteket negativan befolyasolja.
                    //

                    if (!Args.Any(arg => arg.ToLowerInvariant() is "-nolog"))
                        services.Factory<ILogger>(i => ConsoleLogger.Create<AppHost>(), Lifetime.Singleton);
                });
        }
    }
}
