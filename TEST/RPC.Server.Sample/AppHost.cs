/********************************************************************************
* AppHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;

namespace Solti.Utils.Rpc.Server.Sample
{
    using Interfaces;
    using Hosting;

    public class AppHost : AppHostBase
    {
        public override string Name => "Calculator";

        public override string Url => "http://localhost:1986/api/";

        public AppHost() : base()
        {
            RpcService.AllowedOrigins.Add("http://localhost:1987");

            //
            // A naplozas kikapcsolhato mivel az a teljesitmeny teszteket negativan befolyasolja.
            //

            if (Environment.GetCommandLineArgs().Any(arg => arg.ToLowerInvariant() == "-nolog"))
            {
                RpcService.LoggerFactory = null;
                Logger = null;
            }
            else
            {
                RpcService.LoggerFactory = ConsoleLogger.Create<AppHost>;
                Logger = RpcService.LoggerFactory();
            }
        }

        public override void OnRegisterModules(IModuleRegistry registry)
        {
            base.OnRegisterModules(registry);
            registry.Register<ICalculator, Calculator>();
        }
    }
}
