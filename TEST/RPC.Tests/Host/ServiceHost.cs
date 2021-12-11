/********************************************************************************
* ServiceHost.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Server.Sample.Interfaces;

    [TestFixture]
    public class ServiceHostTests: HostTestsBase
    {
        [OneTimeSetUp]
        public async Task Setup()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;

            InvokeAndWait(SAMPLE_SERVER_EXE_PATCH, "service install");
            InvokeAndWait("sc", "start Calculator");

            await WaitForService();
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;

            InvokeAndWait("sc", "stop Calculator");
            InvokeAndWait(SAMPLE_SERVER_EXE_PATCH, "service uninstall");
        }

        [Test]
        public async Task InstalledService_ShouldRun() 
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                Assert.Ignore("The related feature is Windows exclusive.");

            using RpcClientFactory factory = new(HOST);
            ICalculator calculator = await factory.CreateClient<ICalculator>();

            Assert.That(await calculator.AddAsync(1, 1), Is.EqualTo(2));
        }
    }
}
