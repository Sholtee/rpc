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
        public void Context_ShouldBeAccessible() 
        {
            using IServiceContainer container = new ServiceContainer();
            using RpcService server = new RpcService(container);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule.Setup(i => i.Dummy());

            IRequestContext context = null;

            server.Register(injector => 
            {
                context = injector.Get<IRequestContext>();

                return mockModule.Object;
            });

            server.Start(Host);

            var client = new RpcClient<IModule>(Host, "cica");
            client.Proxy.Dummy();

            Assert.That(context, Is.Not.Null);
            Assert.That(context.SessionId, Is.EqualTo("cica"));
            Assert.That(context.Module, Is.EqualTo(nameof(IModule)));
            Assert.That(context.Method, Is.EqualTo(nameof(IModule.Dummy)));
            Assert.That(context.Args, Is.EqualTo("[]"));
        }

        [Test]
        public void RemoteAdd_ShouldWork() 
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

            Assert.That(client.Proxy.Add(1, 2), Is.EqualTo(3));

            mockModule.Verify(i => i.Add(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public async Task RemoteAddAsync_ShouldWork()
        {
            using IServiceContainer container = new ServiceContainer();
            using RpcService server = new RpcService(container);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => Task<int>.Factory.StartNew(() => a + b));

            server.Register(i => mockModule.Object);
            server.Start(Host);

            var client = new RpcClient<IModule>(Host, null);

            Assert.That(await client.Proxy.AddAsync(1, 2), Is.EqualTo(3));

            mockModule.Verify(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }
    }
}
