/********************************************************************************
* AppHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Server.Sample
{
    using Hosting;

    public class AppHost : AppHostBase
    {
        public override string Name => "Calculator";

        public override string Url => "http://localhost:1986/api/";

        public AppHost() : base()
        {
            RpcService.AllowedOrigins.Add("http://localhost:1987");
        }

        public override void OnRegisterModules(IModuleRegistry registry)
        {
            base.OnRegisterModules(registry);
            registry.Register<ICalculator, Calculator>();
        }
    }
}
