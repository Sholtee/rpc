/********************************************************************************
* Rpc.cs                                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI;

    [TestFixture]
    public class RpcTests
    {
        public interface IModule 
        {
            void Dummy();
            void Faulty();
            int Add(int a, int b);
            Task<int> AddAsync(int a, int b);
            Stream GetStream();
            Task<Stream> GetStreamAsync();
        }

        const string Host = "http://127.0.0.1:1986/test/";

        public RpcService Server { get; set; }

        [SetUp]
        public void Setup() => Server = new RpcService(new ServiceContainer());

        [TearDown]
        public void Teardown() 
        {
            Server.Dispose();
            Server.Container.Dispose();
            Server = null;
        }

        [Test]
        public void Context_ShouldBeAccessible() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule.Setup(i => i.Dummy());

            IRequestContext context = null;

            Server.Register(injector => 
            {
                context = injector.Get<IRequestContext>();

                return mockModule.Object;
            });
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host, "cica");
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
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Add(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => a + b);

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host);

            Assert.That(client.Proxy.Add(1, 2), Is.EqualTo(3));

            mockModule.Verify(i => i.Add(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public async Task MultipleRemoteAdd_ShouldWork()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Add(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => a + b);

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            await Task.WhenAll
            (
                Enumerable.Repeat(0, 10).Select
                (
                    _ => Task.Factory.StartNew(() =>
                    {
                        using var client = new RpcClient<IModule>(Host);

                        Assert.That(client.Proxy.Add(1, 2), Is.EqualTo(3));
                    })
                ).ToArray()
            );

            mockModule.Verify(i => i.Add(1, 2), Times.Exactly(10));
        }

        [Test]
        public async Task RemoteAddAsync_ShouldWork()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => Task<int>.Factory.StartNew(() => a + b));

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host);

            Assert.That(await client.Proxy.AddAsync(1, 2), Is.EqualTo(3));

            mockModule.Verify(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void Client_ShouldHandleRemoteExceptions() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Callback(() => throw new InvalidOperationException("cica"));

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host);

            var ex = Assert.Throws<RpcException>(client.Proxy.Faulty);
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException.Message, Is.EqualTo("cica"));
        }

        [Test]
        public void GetStream_ShouldWork() 
        {
            var stm = new MemoryStream();
            byte[] bytes = Encoding.UTF8.GetBytes("kutya");
            stm.Write(bytes, 0, bytes.Length);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.GetStream())
                .Returns(stm);

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host);

            Stream sentStm = null;

            Assert.DoesNotThrow(() => sentStm = client.Proxy.GetStream());
            Assert.That(new StreamReader(sentStm).ReadToEnd, Is.EqualTo("kutya"));

            //
            // Kiszolgalo ldalon a Stream fel lett szabaditva.
            //

            Assert.Throws<ObjectDisposedException>(() => stm.Seek(0, SeekOrigin.Begin));
        }


        [Test]
        public void GetStreamAsync_ShouldWork()
        {
            var stm = new MemoryStream();
            byte[] bytes = Encoding.UTF8.GetBytes("kutya");
            stm.Write(bytes, 0, bytes.Length);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.GetStreamAsync())
                .Returns(Task.FromResult((Stream) stm));

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host);

            Stream sentStm = null;

            Assert.DoesNotThrowAsync(async () => sentStm = await client.Proxy.GetStreamAsync());
            Assert.That(new StreamReader(sentStm).ReadToEnd, Is.EqualTo("kutya"));
            Assert.Throws<ObjectDisposedException>(() => stm.Seek(0, SeekOrigin.Begin));
        }

        [Test]
        public void Server_ShouldTimeout() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Callback(new ManualResetEventSlim().Wait);

            Server.Timeout = TimeSpan.FromSeconds(1);
            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host);

            Assert.Throws<HttpRequestException>(client.Proxy.Faulty);
        }

        [Test]
        public void Client_ShouldTimeout() 
        {
            using var evt = new ManualResetEventSlim();

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Callback(evt.Wait);

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            using var client = new RpcClient<IModule>(Host);
            client.Timeout = TimeSpan.FromSeconds(1);

            Assert.Throws<TaskCanceledException>(client.Proxy.Faulty);
        }

        public interface IDummy 
        {
            void Method_1();
            void Method_2();
        }

        [Test]
        public void Server_ShouldProcessRequestAsynchronously() 
        {
            var evt = new ManualResetEventSlim();

            var mockModule = new Mock<IDummy>(MockBehavior.Strict);

            mockModule
                .Setup(i => i.Method_1())
                .Callback(evt.Wait);

            mockModule.Setup(i => i.Method_2());

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            Task t1 = Task.Factory.StartNew(() => 
            {
                using var client = new RpcClient<IDummy>(Host);
                client.Proxy.Method_1();
            });

            Thread.Sleep(100);

            using var client = new RpcClient<IDummy>(Host);

            //
            // Ha kiszolgalo oldalon a feldolgozas szinkron akkor ez itt deadlock
            //

            client.Proxy.Method_2();

            evt.Set();
            t1.Wait(TimeSpan.FromSeconds(5));
        }

        public interface IGetMyHeaderBack 
        {
            string GetMyHeaderBack();
        }

        public class GetMyHeaderBack : IGetMyHeaderBack
        {
            public IRequestContext Context { get; }

            public GetMyHeaderBack(IRequestContext context) => Context = context;

            string IGetMyHeaderBack.GetMyHeaderBack() => Context.Headers["cica"];
        }

        [Test]
        public void Client_MaySendCustomHeaders() 
        {
            Server.Register<IGetMyHeaderBack, GetMyHeaderBack>();
            Server.Start(Host);

            using var client = new RpcClient<IGetMyHeaderBack>(Host);
            client.CustomHeaders.Add("cica", "mica");

            Assert.That(client.Proxy.GetMyHeaderBack(), Is.EqualTo("mica"));
        }
    }
}
