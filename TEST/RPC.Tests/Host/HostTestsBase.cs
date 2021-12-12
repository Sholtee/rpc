/********************************************************************************
* HostTestsBase.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Server.Sample.Interfaces;

    public class HostTestsBase
    {
        protected const string HOST = "http://localhost:1986/api/";

        protected readonly string SAMPLE_SERVER_EXE_PATCH = Path.ChangeExtension(typeof(ICalculator).Assembly.Location.Replace(".Interfaces", string.Empty, StringComparison.OrdinalIgnoreCase), "exe");

        protected static void InvokeAndWait(string cmd, string args)
        {
            ProcessStartInfo psi = new(cmd)
            {
                Verb = "runas",
                Arguments = args,
                UseShellExecute = true
            };

            Process proc = Process.Start(psi);
            proc.WaitForExit();

            Assert.That(proc.ExitCode, Is.EqualTo(0));
        }

        protected static Process Invoke(string cmd, string args)
        {
            ProcessStartInfo psi = new(cmd)
            {
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            return Process.Start(psi);
        }

        protected static async Task WaitForService()
        {
            using var factory = new RpcClientFactory(HOST);
            int attempts = 0;
            do
            {
                try
                {
                    await factory.ServiceVersion;
                    break;
                }
                catch
                {
                    if (++attempts == 5) throw;
                    Thread.Sleep(50);
                }
            }
            while (true);
        }
    }
}
