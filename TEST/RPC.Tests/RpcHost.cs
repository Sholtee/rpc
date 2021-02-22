/********************************************************************************
* RpcHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using Interfaces;
    using Properties;
    using Server.Sample;

    [TestFixture]
    public class RpcHostTests
    {
        const string Host = "http://localhost:1986/api/";

        public Process HostProcess { get; private set; }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                Arguments = Path.ChangeExtension(typeof(ICalculator).Assembly.Location, "dll"),
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            HostProcess = new Process();
            HostProcess.StartInfo = psi;

            var evt = new ManualResetEventSlim();

            HostProcess.OutputDataReceived += (sender, data) =>
            {
                if (data.Data?.Contains(Trace.RUNNING) == true)
                    evt.Set();
            };

            HostProcess.Start();
            HostProcess.BeginOutputReadLine();
            evt.Wait();
        }

        [OneTimeTearDown]
        public void OneTimeCleanup()
        {
            HostProcess?.Kill();
            HostProcess = null;
        }

        [Test]
        public async Task Client_MayGetTheServiceVersion()
        {
            using var clientFactory = new RpcClientFactory(Host);

            //
            // Mivel a kiszolgalo "dotnet.exe"-vel lett inditva es a verzioinfo mindig a host exe verzioja
            // ezert ez most it gyakorlatilag a runtime verziojat kene visszaadja.
            //

            Version
                serverVer = await clientFactory.ServiceVersion,
                corVer = HostProcess.MainModule.FileVersionInfo;

            Assert.That(serverVer, Is.EqualTo(corVer));
        }

        [Test]
        public async Task ParallelCall_ShouldWork()
        {
            await Task.WhenAll
            (
                Enumerable
                    .Repeat<Func<Task>>(Invoke, 10)
                    .Select(_ => _())
            );

            static async Task Invoke()
            {
                using var factory = new RpcClientFactory(Host)
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };

                ICalculator proxy = await factory.CreateClient<ICalculator>();     
                Assert.That(proxy.Add(1, 2), Is.EqualTo(3));
            }
        }

        [Test]
        public async Task ParallelAsyncCall_ShouldWork()
        {
            await Task.WhenAll
            (
                Enumerable
                    .Repeat<Func<Task>>(Invoke, 10)
                    .Select(_ => _())
            );

            static async Task Invoke()
            {
                using var factory = new RpcClientFactory(Host)
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };

                ICalculator proxy = await factory.CreateClient<ICalculator>();
                Assert.That(await proxy.AddAsync(1, 2), Is.EqualTo(3));
            }
        }
    }
}
