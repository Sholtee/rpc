/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI.Interfaces;
    using Interfaces;
    using Pipeline;
    using Servers;

    [TestFixture]
    public class WebServiceTests
    {
        const string 
            Hello   = "Hello World",
            TestUrl = "http://127.0.0.1:1986/test/";

        static async Task WriteResponseString(IHttpResponse response, string responseString)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            await response.Payload.WriteAsync(buffer, 0, buffer.Length);
        }

        private sealed class RequestDelegatorHandler : IRequestHandler
        {
            public IRequestHandler Next => throw new NotImplementedException();

            public Func<IHttpSession, CancellationToken, Task> OnRequest { get; }

            public Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation) => OnRequest(context, cancellation);

            public RequestDelegatorHandler(Func<IHttpSession, CancellationToken, Task> onRequest) => OnRequest = onRequest;
        }

        private sealed class RequestDelegator : RequestHandlerFactory
        {
            public Func<IHttpSession, CancellationToken, Task> Handler { get; private set; } = async (context, _) =>
            {
                IHttpResponse response = context.Response;

                response.Headers["Content-Type"] = "text/html";
                response.StatusCode = HttpStatusCode.OK;

                await WriteResponseString(response, Hello);

                await response.Close();
            };

            public void SetHandler(Func<IHttpSession, CancellationToken, Task> handler) => Handler = handler;

            protected override IRequestHandler Create(IRequestHandler next) => new RequestDelegatorHandler(Handler);
        }

        private WebService Svc { get; set; }

        private static WebServiceBuilder CreateBuilder(Action<RequestHandlerFactory> config = null) => new WebServiceBuilder()
            .ConfigureBackend(_ => new HttpListenerBackend(TestUrl) { ReserveUrl = true })
            .ConfigurePipeline(pipe => pipe
                .Use<RequestDelegator>(config)
                .Use<RequestTimeout>(config)
                .Use<HttpAccessControl>(config)
                .Use<RequestLimiter>(config)
                .Use<ExceptionCatcher>());

        private static WebService CreateService(Action<RequestHandlerFactory> config = null) => CreateBuilder(config).Build();

        [TearDown]
        public void TeardownFixture() 
        {
            Svc?.Dispose();
            Svc = null;
        }

        private static async Task InvokeService()
        {
            using HttpClient client = new();

            HttpResponseMessage response = await client.GetAsync(TestUrl);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.That(responseBody, Is.EqualTo(Hello));
        }

        [Test]
        public async Task Service_ShouldHandleRequests() 
        {
            Svc = CreateService();
            await Svc.Start();

            await InvokeService();
        }

        [Test]
        public async Task Service_ShouldHandleRequestsAsynchronously()
        {
            Svc = CreateService();
            await Svc.Start();

            Assert.DoesNotThrowAsync(() => Task.WhenAll(Enumerable
                .Repeat<Func<Task>>(InvokeService, 100)
                .Select(_ => _())));
        }

        [Test]
        public async Task Service_ShouldHandleExceptions() 
        {
            Svc = CreateService(conf => (conf as RequestDelegator)?.SetHandler((_, _) => throw new Exception()));
            await Svc.Start();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.That(responseBody, Is.EqualTo(new Exception().Message));
        }

        [Test]
        public async Task Service_CanBeRestarted() 
        {
            Svc = CreateService();
            await Svc.Start();

            Assert.That(Svc.IsStarted);
            Assert.That(Svc.IsListening);

            Assert.DoesNotThrowAsync(Svc.Stop);

            Assert.That(!Svc.IsStarted);
            Assert.That(!Svc.IsListening);

            Assert.DoesNotThrowAsync(Svc.Start);

            Assert.That(Svc.IsStarted);
            Assert.That(Svc.IsListening);

            //
            // ujrainditas utan is mukodik
            //

            await InvokeService();
        }

        [Test]
        public void Start_ShouldValidateTheUrl() 
        {
            WebServiceBuilder bldr = new WebServiceBuilder().ConfigureBackend(_ => new HttpListenerBackend("invalid"));
            Assert.Throws<ArgumentException>(() => bldr.Build());
        }

        [Test]
        public async Task Service_ShouldReturnHttpOkByDefault() 
        {
            Svc = new WebServiceBuilder().ConfigureBackend(_ => new HttpListenerBackend(TestUrl)).Build();
            await Svc.Start();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await response.Content.ReadAsStreamAsync()).Length, Is.EqualTo(0));
        }

        [Test]
        public async Task Service_ShouldHandlePreflightRequests()
        {
            const string origin = "http://cica.hu";

            var mockProcessor = new Mock<Func<IHttpSession, CancellationToken, Task>>(MockBehavior.Strict);

            Svc = CreateService(conf =>
            {
                switch (conf)
                {
                    case RequestDelegator delegator:
                        delegator.SetHandler(mockProcessor.Object);
                        break;
                    case HttpAccessControl ac:
                        ac.AllowedOrigins.Add(origin);
                        break;
                }
            });
            await Svc.Start();

            using var client = new HttpClient();

            var req = new HttpRequestMessage(HttpMethod.Options, TestUrl);
            req.Headers.Add("Origin", origin);

            HttpResponseMessage response = await client.SendAsync(req);

            Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin").Single(), Is.EqualTo(origin));
            Assert.That(response.Headers.GetValues("Vary").Single(), Is.EqualTo("Origin"));
            
            mockProcessor.Verify(ctx => ctx(It.IsAny<IHttpSession>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private async Task Service_ShouldAllowAnyXxXByDefault(string headerName) 
        {
            var mockProcessor = new Mock<Func<IHttpSession, CancellationToken, Task>>(MockBehavior.Strict);

            Svc = CreateService(conf => (conf as RequestDelegator)?.SetHandler(mockProcessor.Object));
            await Svc.Start();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, TestUrl));

            Assert.That(response.Headers.GetValues(headerName).Single(), Is.EqualTo("*"));

            mockProcessor.Verify(ctx => ctx(It.IsAny<IHttpSession>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Service_ShouldAllowAnyHeaderByDefault() => await Service_ShouldAllowAnyXxXByDefault("Access-Control-Allow-Headers");

        [Test]
        public async Task Service_ShouldAllowAnyMethodByDefault() => await Service_ShouldAllowAnyXxXByDefault("Access-Control-Allow-Methods");

        [Test]
        public async Task Service_ShouldTimeout()
        {
            Task processor = null;

            Svc = CreateService(conf => 
            {
                switch (conf)
                {
                    case RequestDelegator delgator:
                        delgator.SetHandler((ctx, cancellation) => processor = Task.Factory.StartNew(() =>
                        {
                            using ManualResetEventSlim evt = new();
                            evt.Wait(cancellation);
                        }, TaskCreationOptions.LongRunning));
                        break;
                    case RequestTimeout timeout:
                        timeout.Timeout = TimeSpan.FromSeconds(1);
                        break;
                }
            });
            await Svc.Start();

            using HttpClient client = new();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(new OperationCanceledException().Message));

            Thread.Sleep(100);
            Assert.That(processor.IsCompleted);
        }

        [Test]
        public async Task Service_ShouldRejectTheRequestIfTheRequestCountReachesTheThreshold()
        {
            Svc = 
                CreateBuilder(conf => 
                {
                    switch (conf)
                    {
                        case RequestLimiter requestLimiter:
                            requestLimiter.Interval = () => TimeSpan.FromSeconds(1);
                            requestLimiter.Threshold = () => 1;
                            break;
                    }
                })
                .Build();
            await Svc.Start();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            response = await client.GetAsync(TestUrl);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

            await Task.Delay(2000);

            response = await client.GetAsync(TestUrl);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}
