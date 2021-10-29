/********************************************************************************
* AppHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Server.Sample
{
    using Hosting;
    using Interfaces;

    using DI.Interfaces;

    public class AppHost : AppHostBase
    {
        public AppHost() => Name = "Calculator";

        public override void OnBuildService(RpcServiceBuilder serviceBuilder) => serviceBuilder
            .ConfigureWebService(new WebServiceDescriptor
            {
                Url = "http://localhost:1986/api/",
                AllowedOrigins = new[] 
                {
                    "http://localhost:1987"
                }
            })
            .ConfigureServices(services =>
            {
                //
                // A naplozas kikapcsolhato mivel az a teljesitmeny teszteket negativan befolyasolja.
                //

                if (!Environment.GetCommandLineArgs().Any(arg => arg.ToLowerInvariant() is "-nolog"))
                {
                    services.Factory<ILogger>(i => ConsoleLogger.Create<AppHost>(), Lifetime.Singleton);
                }
            })
            .ConfigureModules(modules => modules.Register<ICalculator, Calculator>());
    }
}
