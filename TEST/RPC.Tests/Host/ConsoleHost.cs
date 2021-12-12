/********************************************************************************
* ConsoleHost.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    [TestFixture]
    public class ConsoleHostTests: HostTestsBase
    {
        [Test]
        public async Task Runner_ShouldTerminateOnCtrlC() 
        {
            Process proc = Invoke(SAMPLE_SERVER_EXE_PATCH, string.Empty);

            await WaitForService();

            proc.StandardInput.WriteLine('\x3');

            Assert.That(proc.WaitForExit(2000));
        }
    }
}
