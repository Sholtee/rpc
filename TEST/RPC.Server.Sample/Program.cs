/********************************************************************************
* Program.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Server.Sample
{
    using Hosting;

    class Program
    {
        static void Main() => HostRunner.Run<AppHost>();
    }
}
