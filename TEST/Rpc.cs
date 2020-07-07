/********************************************************************************
* Rpc.cs                                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI;
    using DI.Interfaces;

    [TestFixture]
    public class RpcTests
    {
        public interface IModule 
        {
            public void Dummy();
            public void Faulty();
            public int Add(int a, int b);
            public Task<int> AddAsync(int a, int b);
        }

        const string Host = "http://127.0.0.1:1986/test/";

        [Test]
        public void RemoteAdd() 
        {
            using IServiceContainer container = new ServiceContainer();
            using RpcService server = new RpcService(container);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Add(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => a + b);

            server.Register(i => mockModule.Object);
            server.Start(Host);

            var client = new RpcClient<IModule>(Host, null);
            client.Timeout = TimeSpan.FromMinutes(2);

            Assert.That(client.Proxy.Add(1, 2), Is.EqualTo(3));
        }
    }
}
