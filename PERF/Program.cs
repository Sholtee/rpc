/********************************************************************************
* Program.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.IO;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Solti.Utils.Rpc.Perf
{
    using Server.Sample.Interfaces;

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
            var psi = new ProcessStartInfo(Path.ChangeExtension(typeof(ICalculator).Assembly.Location.Replace(".Interfaces", string.Empty, StringComparison.OrdinalIgnoreCase), "exe"))
            {
                UseShellExecute = true,
                Arguments = "-nolog"
            };

            return Process.Start(psi);
        }
    }
}
