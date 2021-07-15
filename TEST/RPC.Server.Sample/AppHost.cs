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
    using DI.Interfaces;
    using Interfaces;
    using Hosting;
    using Rpc.Interfaces;

    public class AppHost : AppHostBase
    {
        public override string Name => "Calculator";

        public override string Url => "http://localhost:1986/api/";

        public AppHost() : base()
        {
            RpcService.AllowedOrigins.Add("http://localhost:1987");
        }

        public override void OnRegisterServices(IServiceContainer container)
        {
            base.OnRegisterServices(container);

            //
            // A naplozas kikapcsolhato mivel az a teljesitmeny teszteket negativan befolyasolja.
            //

            if (!Environment.GetCommandLineArgs().Any(arg => arg.ToLowerInvariant() == "-nolog"))
            {
                container.Factory<ILogger>(i => ConsoleLogger.Create<AppHost>(), Lifetime.Singleton);
            }
        }

        public override void OnRegisterModules(IModuleRegistry registry)
        {
            base.OnRegisterModules(registry);
            registry.Register<ICalculator, Calculator>();
        }
    }
}
