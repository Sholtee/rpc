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

        public override string Url => "http://127.0.0.1:1986/api/";

        public override void OnRegisterModules(IModuleRegistry registry)
        {
            base.OnRegisterModules(registry);
            registry.Register<ICalculator, Calculator>();
        }
    }
}
