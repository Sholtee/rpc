/********************************************************************************
* Program.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Server.Sample
{
    class Program
    {
        static void Main()
        {
            using AppHost appHost = new AppHost();
            appHost.Prepare();
            appHost.Runner.Start();
        }
    }
}
