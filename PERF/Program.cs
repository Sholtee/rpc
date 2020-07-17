/********************************************************************************
* Program.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;
using System.IO;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Solti.Utils.Rpc.Perf
{
    using Server.Sample;

    class Program
    {
        static void Main(string[] args)
        {
            Process server = StartServer();
            try
            {
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run
                (
                    args
#if DEBUG
                , new DebugInProcessConfig()
#endif
                );
            }
            finally
            {
                server.Kill();
            }
        }

        private static Process StartServer() 
        {
            var psi = new ProcessStartInfo(Path.ChangeExtension(typeof(ICalculator).Assembly.Location, "exe"))
            {
                UseShellExecute = true
            };

            return Process.Start(psi);
        }
    }
}
