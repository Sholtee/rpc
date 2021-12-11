/********************************************************************************
* ConsoleHost.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    [TestFixture]
    public class ConsoleHostTests: HostTestsBase
    {
        public enum ConsoleCtrlEvent
        {
            CTRL_C = 0
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, int dwProcessGroupId);


        [Test, Ignore("Make it work")]
        public async Task Runner_ShouldTerminateOnCtrlC() 
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                Assert.Ignore("The related feature is Windows exclusive.");

            Process proc = Invoke(SAMPLE_SERVER_EXE_PATCH, string.Empty);

            await WaitForService();

            Assert.That(GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, 0));

            Assert.That(proc.WaitForExit(2000));
        }
    }
}
