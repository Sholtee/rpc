﻿/********************************************************************************
* Rpc.cs                                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI;
    using DI.Interfaces;
    using Interfaces;
    using Pipeline;
    using Servers;

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
            Complex MethodHavingComplexArg(Complex arg);
            int Prop { get; set; }
            Guid Guid { get; }
        }

        const string Host = "http://localhost:1986/test/";

        public WebService Server { get; set; }

        public WebServiceBuilder ServerBuilder { get; set; }

        public RpcClientFactory ClientFactory { get; set; }

        [SetUp]
        public void Setup()
        {
            ServerBuilder = new WebServiceBuilder().ConfigureBackend(_ => new HttpListenerBackend(Host) { ReserveUrl = true });
            ClientFactory = new RpcClientFactory(Host);
        }

        protected Task StartServer()
        {
            Server = ServerBuilder.Build();
            return Server.Start();
        }

        [TearDown]
        public void Teardown() 
        {
            Server?.Dispose();
            Server = null;

            ClientFactory.Dispose();
            ClientFactory = null;
        }

        [Test]
        public async Task Context_ShouldBeAccessible() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule.Setup(i => i.Dummy());

            IRpcRequestContext context = null;

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => 
            {
                context = injector.Get<IRpcRequestContext>();

                return mockModule.Object;
            }));
            await StartServer();

            ClientFactory.SessionId = "cica";

            IModule proxy = await ClientFactory.CreateClient<IModule>();
            proxy.Dummy();

            Assert.That(context, Is.Not.Null);
            Assert.That(context.SessionId, Is.EqualTo("cica"));
            Assert.That(context.Module, Is.EqualTo(nameof(IModule)));
            Assert.That(context.Method, Is.EqualTo(nameof(IModule.Dummy)));
        }

        [Test]
        public async Task Logger_ShouldBeAccessible() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule.Setup(i => i.Dummy());

            ILogger logger = null;

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector =>
            {
                logger = injector.Get<ILogger>();

                return mockModule.Object;
            }));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();
            proxy.Dummy();

            Assert.That(logger, Is.Not.Null);
        }

        [Test]
        public async Task RemoteAdd_ShouldWork() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Add(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => a + b);

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();
            Assert.That(proxy.Add(1, 2), Is.EqualTo(3));

            mockModule.Verify(i => i.Add(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public async Task ParallelCalls_ShouldWork()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Add(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => a + b);

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            await Task.WhenAll
            (
                Enumerable
                    .Repeat<Func<Task>>(Invoke, 10)
                    .Select(_ => _())
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
        public async Task ParallelAsyncCalls_ShouldWork()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => Task.FromResult(a + b));

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            await Task.WhenAll
            (
                Enumerable
                    .Repeat<Func<Task>>(Invoke, 10)
                    .Select(_ => _())
            );

            mockModule.Verify(i => i.AddAsync(1, 2), Times.Exactly(10));

            static async Task Invoke()
            {
                using var factory = new RpcClientFactory(Host);
                IModule proxy = await factory.CreateClient<IModule>();
                Assert.That(await proxy.AddAsync(1, 2), Is.EqualTo(3));
            }
        }

        [Test]
        public async Task RemoteAddAsync_ShouldWork()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((a, b) => Task<int>.Factory.StartNew(() => a + b));

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

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
                .SetupSet(i => i.Prop = 1986)
                .Callback<int>(val => prop = val);

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

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

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

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
                .Returns(Task.FromException(new InvalidOperationException("cica")));
            mockModule
                .Setup(i => i.AddAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.FromException<int>(new InvalidOperationException("cica")));

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            var ex = Assert.ThrowsAsync<RpcException>(proxy.Faulty);
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException.Message, Is.EqualTo("cica"));

            ex = Assert.ThrowsAsync<RpcException>(() => proxy.AddAsync(0, 0));
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException.Message, Is.EqualTo("cica"));
        }

        [Test]
        public async Task Client_ShouldAccessDataFromTheRemoteException()
        {
            Exception ex = new InvalidOperationException("cica");
            ex.Data["mica"] = "kutya";

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Returns(Task.FromException(ex));

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            ex = Assert.ThrowsAsync<RpcException>(proxy.Faulty);
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException.Message, Is.EqualTo("cica"));
            Assert.That(ex.InnerException.Data["mica"], Is.EqualTo("kutya"));
        }

        [Test]
        public async Task Client_ShouldHandleRemoteExceptionsThrownByMethodReturningAValueType()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Add(1, 1))
                .Callback(() => throw new InvalidOperationException("cica"));

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            var ex = Assert.Throws<RpcException>(() => proxy.Add(1, 1));
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

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Stream sentStm = null;

            Assert.DoesNotThrow(() => sentStm = proxy.GetStream());
            Assert.That(new StreamReader(sentStm).ReadToEnd, Is.EqualTo("kutya"));

            //
            // Kiszolgalo oldalon a Stream fel lett szabaditva.
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

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Stream sentStm = null;

            Assert.DoesNotThrowAsync(async () => sentStm = await proxy.GetStreamAsync());
            Assert.That(new StreamReader(sentStm).ReadToEnd, Is.EqualTo("kutya"));
            Assert.Throws<ObjectDisposedException>(() => stm.Seek(0, SeekOrigin.Begin));
        }

        [Test]
        public async Task Server_ShouldTimeout() 
        {
            ServerBuilder.ConfigureRpcService(conf =>
            {
                switch (conf)
                {
                    case Modules modules:
                        modules.Register(injector =>
                        {
                            var mockModule = new Mock<IModule>(MockBehavior.Strict);
                            mockModule
                                .Setup(i => i.Faulty())
                                .Returns(Task.Factory.StartNew(() => new ManualResetEventSlim().Wait(injector.Get<IRpcRequestContext>().Cancellation)));

                            return mockModule.Object;
                        });
                        break;
                    case RequestTimeout timeout:
                        timeout.Timeout = TimeSpan.FromSeconds(1);
                        break;
                }
            });
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            var ex = Assert.ThrowsAsync<RpcException>(() => proxy.Faulty());
            Assert.That(ex.InnerException, Is.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task Server_ShouldValidateTheRequest() 
        {
            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => new Mock<IModule>(MockBehavior.Strict).Object));
            await StartServer();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.DeleteAsync(Host);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));

            response = await client.PostAsync(Host, new StringContent(string.Empty));
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
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

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            var ex = Assert.ThrowsAsync<HttpRequestException>(proxy.Faulty);
            Assert.That(ex.Message.Contains("401"));
        }

        public interface IGuid 
        {
            Guid Value { get; }
        }

        public static IEnumerable<Lifetime> Lifetimes
        {
            get
            {
                Debug.WriteLine($"Loading assembly: {typeof(ServiceCollection).Assembly}");

                yield return Lifetime.Transient;
                yield return Lifetime.Scoped;
                yield return Lifetime.Singleton;
                yield return Lifetime.Pooled.WithCapacity(4);
            }
        }

        [Test]
        public async Task Module_MayHaveDependencies([ValueSource(nameof(Lifetimes))] Lifetime lifetime) 
        {
            int factoryRequested = 0;
            ServerBuilder.ConfigureServices(svcs => svcs.Factory(injector => 
            {
                var mockService = new Mock<IGuid>(MockBehavior.Strict);
                mockService
                    .SetupGet(svc => svc.Value)
                    .Returns(Guid.NewGuid());

                factoryRequested++;
                return mockService.Object;
            }, lifetime));

            int moduleRequested = 0;
            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector =>
            {
                var mockModule = new Mock<IModule>(MockBehavior.Strict);
                mockModule
                    .SetupGet(i => i.Guid)
                    .Returns(() => injector.Get<IGuid>().Value);

                moduleRequested++;
                return mockModule.Object;
            }));

            await StartServer();

            for (int i = 0; i < 20; i++)
                await InvokeModule();

            Assert.That(moduleRequested, Is.EqualTo(20));

            Assert.That(factoryRequested, lifetime.ToString() switch
            {
                nameof(Lifetime.Singleton) => Is.EqualTo(1),
                nameof(Lifetime.Pooled) => Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(4),
                _ => Is.EqualTo(20)
            });

            async Task InvokeModule() 
            {
                IModule proxy = await ClientFactory.CreateClient<IModule>();
                _ = proxy.Guid;
            }
        }

        [Test]
        public async Task Client_ShouldTimeout() 
        {
            using var evt = new ManualResetEventSlim();

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(i => i.Faulty())
                .Returns(Task.Factory.StartNew(evt.Wait));

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            ClientFactory.Timeout = TimeSpan.FromSeconds(1);

            IModule proxy = await ClientFactory.CreateClient<IModule>();

            Assert.ThrowsAsync<TaskCanceledException>(() => proxy.Faulty());

            evt.Set();
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

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

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
            public IRpcRequestContext Context { get; }

            public GetMyHeaderBack(IRpcRequestContext context) => Context = context;

            string IGetMyHeaderBack.GetMyHeaderBack() => Context.OriginalRequest.Headers["cica"];
        }

        [Test]
        public async Task Client_MaySendCustomHeaders() 
        {
            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register<IGetMyHeaderBack, GetMyHeaderBack>());
            await StartServer();

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
            public IRpcRequestContext Context { get; }

            public GetMyParamBack(IRpcRequestContext context) => Context = context;

            string IGetMyParamBack.GetMyParamBack() => Context.OriginalRequest.QueryParameters["cica"];
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
            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register<IGetMyParamBack, GetMyParamBack>());
            await StartServer();

            using var clientFactory = new MyFactory(Host);

            Assert.That((await clientFactory.CreateClient<IGetMyParamBack>()).GetMyParamBack(), Is.EqualTo("mica"));
        }

        [Test]
        public async Task Client_MustNotAccessModuleDependencies() 
        {
            var mockDisposable = new Mock<IDisposable>(MockBehavior.Strict);
            mockDisposable.Setup(d => d.Dispose());

            ServerBuilder.ConfigureRpcService(_ => { }).ConfigureServices(svcs => svcs.Factory(i => mockDisposable.Object, Lifetime.Transient));
            await StartServer();

            IDisposable proxy = await ClientFactory.CreateClient<IDisposable>();

            var ex = Assert.Throws<RpcException>(proxy.Dispose);
            Assert.That(ex.InnerException, Is.InstanceOf<MissingModuleException>());
            mockDisposable.Verify(d => d.Dispose(), Times.Never);
        }

        public class Complex 
        {
            public string PropA { get; set; }
            public int PropB { get; set; }
        }

        [Test]
        public async Task Serialization_ShouldBeControlled() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.MethodHavingComplexArg(It.IsAny<Complex>()))
                .Returns<Complex>(arg => arg);

            ServerBuilder.ConfigureRpcService(conf => 
            {
                switch (conf)
                {
                    case Modules modules:
                        modules
                            .ConfigureSerializer(_ => new JsonSerializerBackend 
                            {
                                Options = new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = new LowerCasePolicy()
                                }
                            })
                            .Register(i => mockModule.Object);
                        break;
                }
            });
            await StartServer();

            IModule proxy = await ClientFactory.CreateClient<IModule>();
            Complex ret = proxy.MethodHavingComplexArg(new Complex { PropA = "cica", PropB = 1986 });

            // kisbetu miatt {"result": ...} formaban lesz a valasz
            Assert.That(ret, Is.Null);

            ClientFactory.SerializerOptions = new JsonSerializerOptions(); // ne legyen InvalidOperationException (mivel a korabbi beallitasok mar hasznalva vtak) 
            ClientFactory.SerializerOptions.PropertyNamingPolicy = new LowerCasePolicy();
            proxy = await ClientFactory.CreateClient<IModule>();

            ret = proxy.MethodHavingComplexArg(new Complex { PropA = "cica", PropB = 1986 });

            Assert.That(ret.PropA, Is.EqualTo("cica"));
            Assert.That(ret.PropB, Is.EqualTo(1986));
        }

        private class LowerCasePolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name) => name.ToLowerInvariant();
        }

        [Test]
        public async Task Request_ShouldFailOnBadQueryParameters()
        {
            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => new Mock<IModule>(MockBehavior.Strict).Object));
            await StartServer();

            using BadRpcClientFactory clientFactory = new(Host);

            IModule module = await clientFactory.CreateClient<IModule>();

            HttpRequestException ex = Assert.Throws<HttpRequestException>(() => module.Add(1, 1));
            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        private class BadRpcClientFactory : RpcClientFactory
        {
            public BadRpcClientFactory(string host) : base(host)
            {
            }

            protected override IDictionary<string, string> GetRequestParameters(MethodInfo method)
            {
                if (method is null)
                    throw new ArgumentNullException(nameof(method));

                var paramz = new Dictionary<string, string>
                {
                    { "module_bad", GetMemberId(method.ReflectedType) },
                    { "method", GetMemberId(method)}
                };

                if (SessionId != null) paramz.Add("sessionid", SessionId);

                return paramz;
            }
        }

        [Test]
        public async Task Client_MayRequestPublishedApiSchemaOnly()
        {
            //
            // Nincs rajt PublishSchema
            //

            var mockModule = new Mock<IModule>(MockBehavior.Strict);

            ServerBuilder.ConfigureRpcService(conf => (conf as Modules)?.Register(injector => mockModule.Object));
            await StartServer();

            using var client = new HttpClient();

            //
            // IServiceDescriptor alapbol regisztralva van es publikalt is a semaja
            //

            HttpResponseMessage response = await client.GetAsync(QueryHelpers.AddQueryString(Host, "module", "IServiceDescriptor"));

            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));

            string s = await response.Content.ReadAsStringAsync();
            Assert.That(s, Is.EqualTo(@"{""IServiceDescriptor"":{""Methods"":{},""Properties"":{""Name"":{""Layout"":""TODO"",""HasGetter"":true,""HasSetter"":false},""Version"":{""Layout"":""TODO"",""HasGetter"":true,""HasSetter"":false}}}}"));

            response = await client.GetAsync(QueryHelpers.AddQueryString(Host, "module", "IModule"));

            s = await response.Content.ReadAsStringAsync();
            Assert.That(s, Is.EqualTo("{}"));
        }
    }
}
