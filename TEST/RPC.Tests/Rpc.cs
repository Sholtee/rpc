/********************************************************************************
* Rpc.cs                                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI;
    using Interfaces;

    [TestFixture]
    public class RpcTests
    {
        public interface IModule 
        {
            void Dummy();
            Task Faulty();
            int Add(int a, int b);
            Task<int> AddAsync(int a, int b);
            Task Async();
            Stream GetStream();
            Task<Stream> GetStreamAsync();
            int Prop { get; set; }
        }

        const string Host = "http://localhost:1986/test/";

        public RpcService Server { get; set; }

        public RpcClientFactory ClientFactory { get; set; }

        [SetUp]
        public void Setup()
        {
            Server = new RpcService(new ServiceContainer());
            ClientFactory = new RpcClientFactory(Host);
        }

        [TearDown]
        public void Teardown() 
        {
            Server.Dispose();
            Server.Container.Dispose();
            Server = null;

            ClientFactory.Dispose();
            ClientFactory = null;
        }

        [Test]
        public async Task Context_ShouldBeAccessible() 
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

            ClientFactory.SessionId = "cica";

            IModule proxy = await ClientFactory.CreateClient<IModule>();
            proxy.Dummy();

            Assert.That(context, Is.Not.Null);
            Assert.That(context.SessionId, Is.EqualTo("cica"));
            Assert.That(context.Module, Is.EqualTo(nameof(IModule)));
            Assert.That(context.Method, Is.EqualTo(nameof(IModule.Dummy)));
        }

        [Test]
        public async Task RemoteAdd_ShouldWork() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Add(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => a + b);

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            IModule proxy = await ClientFactory.CreateClient<IModule>();
            Assert.That(proxy.Add(1, 2), Is.EqualTo(3));

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
                Enumerable.Repeat(0, 10).Select(_ => Invoke()).ToArray()
            );

            mockModule.Verify(i => i.Add(1, 2), Times.Exactly(10));

            static async Task Invoke() 
            {
                using var factory = new RpcClientFactory(Host);
                IModule proxy = await factory.CreateClient<IModule>();
                Assert.That(proxy.Add(1, 2), Is.EqualTo(3));
            }
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

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Assert.That(await proxy.AddAsync(1, 2), Is.EqualTo(3));

            mockModule.Verify(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public async Task UsingProperties_ShouldWork() 
        {
            int prop = 0;

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .SetupGet(i => i.Prop)
                .Returns(() => prop);
            mockModule
                .SetupSet(i => i.Prop)
                .Callback(val => prop = val);

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Assert.DoesNotThrow(() => proxy.Prop = 1986);
            Assert.That(proxy.Prop, Is.EqualTo(1986));
        }

        [Test]
        public async Task RemoteAsync_ShouldWork()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Async())
                .Returns(Task.CompletedTask);

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Assert.DoesNotThrowAsync(proxy.Async);

            mockModule.Verify(i => i.Async(), Times.Once);
        }

        [Test]
        public async Task Client_ShouldHandleRemoteExceptions() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Callback(() => throw new InvalidOperationException("cica"));

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            var ex = Assert.ThrowsAsync<RpcException>(proxy.Faulty);
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException.Message, Is.EqualTo("cica"));
        }

        [Test]
        public async Task GetStream_ShouldWork() 
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

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Stream sentStm = null;

            Assert.DoesNotThrow(() => sentStm = proxy.GetStream());
            Assert.That(new StreamReader(sentStm).ReadToEnd, Is.EqualTo("kutya"));

            //
            // Kiszolgalo ldalon a Stream fel lett szabaditva.
            //

            Assert.Throws<ObjectDisposedException>(() => stm.Seek(0, SeekOrigin.Begin));
        }

        [Test]
        public async Task GetStreamAsync_ShouldWork()
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

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Stream sentStm = null;

            Assert.DoesNotThrowAsync(async () => sentStm = await proxy.GetStreamAsync());
            Assert.That(new StreamReader(sentStm).ReadToEnd, Is.EqualTo("kutya"));
            Assert.Throws<ObjectDisposedException>(() => stm.Seek(0, SeekOrigin.Begin));
        }

        [Test]
        public async Task Server_ShouldTimeout() 
        {
            Server.Timeout = TimeSpan.FromSeconds(1);
            Server.Register(i =>
            {
                var mockModule = new Mock<IModule>(MockBehavior.Strict);
                mockModule
                    .Setup(i => i.Faulty())
                    .Returns(Task.Factory.StartNew(() => new ManualResetEventSlim().Wait(i.Get<IRequestContext>().Cancellation)));

                return mockModule.Object;
            });
            Server.Start(Host);

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            var ex = Assert.ThrowsAsync<RpcException>(() => proxy.Faulty());
            Assert.That(ex.InnerException, Is.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task Server_ShouldValidateTheRequest() 
        {
            Server.Register(i => new Mock<IModule>(MockBehavior.Strict).Object);
            Server.Start(Host);

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(Host);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));

            response = await client.PostAsync(Host, new StringContent(string.Empty));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Module_MaySendArbitraryErrorCode() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Callback(() => throw new HttpException
                {
                    Status = HttpStatusCode.Unauthorized
                });

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            var ex = Assert.ThrowsAsync<HttpRequestException>(proxy.Faulty);
            Assert.That(ex.Message.Contains("401"));
        } 

        [Test]
        public async Task Client_ShouldTimeout() 
        {
            using var evt = new ManualResetEventSlim();

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Returns(Task.Factory.StartNew(evt.Wait));

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            ClientFactory.Timeout = TimeSpan.FromSeconds(1);

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Assert.ThrowsAsync<TaskCanceledException>(() => proxy.Faulty());
        }

        public interface IDummy 
        {
            Task Method_1();
            void Method_2();
        }

        [Test]
        public async Task Server_ShouldProcessRequestAsynchronously() 
        {
            var evt = new ManualResetEventSlim();

            var mockModule = new Mock<IDummy>(MockBehavior.Strict);

            mockModule
                .Setup(i => i.Method_1())
                .Returns(Task.Factory.StartNew(evt.Wait));

            mockModule.Setup(i => i.Method_2());

            Server.Register(i => mockModule.Object);
            Server.Start(Host);

            IDummy proxy = await ClientFactory.CreateClient<IDummy>();

            Task blockingTask = proxy.Method_1();

            Thread.Sleep(100);

            //
            // Ha kiszolgalo oldalon a feldolgozas szinkron akkor ez itt timeoutolni fog
            //

            Assert.DoesNotThrow(proxy.Method_2);

            Assert.That(blockingTask.IsCompleted, Is.False);
            evt.Set();
            
            Assert.That(blockingTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.That(blockingTask.IsCompletedSuccessfully);
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
        public async Task Client_MaySendCustomHeaders() 
        {
            Server.Register<IGetMyHeaderBack, GetMyHeaderBack>();
            Server.Start(Host);

            ClientFactory.CustomHeaders.Add("cica", "mica");
            IGetMyHeaderBack proxy = await ClientFactory.CreateClient<IGetMyHeaderBack>();

            Assert.That(proxy.GetMyHeaderBack(), Is.EqualTo("mica"));
        }

        public interface IGetMyParamBack 
        {
            string GetMyParamBack();
        }

        public class GetMyParamBack : IGetMyParamBack
        {
            public IRequestContext Context { get; }

            public GetMyParamBack(IRequestContext context) => Context = context;

            string IGetMyParamBack.GetMyParamBack() => Context.RequestParameters["cica"];
        }

        public class MyFactory : RpcClientFactory
        {
            public MyFactory(string host) : base(host) { }

            protected override IDictionary<string, string> GetRequestParameters(MethodInfo method)
            {
                IDictionary<string, string> result = base.GetRequestParameters(method);
                result.Add("cica", "mica");
                return result;
            }
        }

        [Test]
        public async Task Client_MaySendCustomParameters()
        {
            Server.Register<IGetMyParamBack, GetMyParamBack>();
            Server.Start(Host);

            using var clientFactory = new MyFactory(Host);

            Assert.That((await clientFactory.CreateClient<IGetMyParamBack>()).GetMyParamBack(), Is.EqualTo("mica"));
        }

        [Test]
        public async Task Client_MustNotAccessModuleDependencies() 
        {
            var mockDisposable = new Mock<IDisposable>(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            Server.Container.Factory(i => mockDisposable.Object);
            Server.Start(Host);

            IDisposable proxy = await ClientFactory.CreateClient<IDisposable>();

            var ex = Assert.Throws<RpcException>(proxy.Dispose);
            Assert.That(ex.InnerException, Is.InstanceOf<MissingModuleException>());
            mockDisposable.Verify(d => d.Dispose(), Times.Never);
        }
    }
}
