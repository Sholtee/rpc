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

    [TestFixture]
    public class WebServiceTests
    {
        const string 
            Hello   = "Hello World",
            TestUrl = "http://127.0.0.1:1986/test/";

        static async Task WriteResponseString(HttpListenerResponse response, string responseString)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private sealed class RequestDelegatorHandler : IRequestHandler
        {
            public IRequestHandler Next => throw new NotImplementedException();

            public Func<RequestContext, Task> OnRequest { get; }

            public Task HandleAsync(RequestContext context) => OnRequest(context);

            public RequestDelegatorHandler(Func<RequestContext, Task> onRequest) => OnRequest = onRequest;
        }

        private sealed class RequestDelegator : RequestHandlerFactory
        {
            public Func<RequestContext, Task> Handler { get; private set; } = async context =>
            {
                HttpListenerResponse response = context.Response;

                response.ContentType = "text/html";
                response.StatusCode = 200;

                await WriteResponseString(response, Hello);

                response.Close();
            };

            public void SetHandler(Func<RequestContext, Task> handler) => Handler = handler;

            protected override IRequestHandler Create(IRequestHandler next) => new RequestDelegatorHandler(Handler);
        }

        private WebService Svc { get; set; }

        private static WebServiceBuilder CreateBuilder(Action<RequestHandlerFactory> config = null) => new WebServiceBuilder { Url = TestUrl }
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
            Svc.Start();

            await InvokeService();
        }

        [Test]
        public void Service_ShouldHandleRequestsAsynchronously()
        {
            Svc = CreateService();
            Svc.Start();

            Assert.DoesNotThrowAsync(() => Task.WhenAll(Enumerable
                .Repeat<Func<Task>>(InvokeService, 100)
                .Select(_ => _())));
        }

        [Test]
        public async Task Service_ShouldHandleExceptions() 
        {
            Svc = CreateService(conf => (conf as RequestDelegator)?.SetHandler(_ => throw new Exception()));
            Svc.Start();

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
            Svc.Start();

            Assert.That(Svc.IsStarted);
            Assert.That(Svc.IsListening);

            Assert.DoesNotThrow(Svc.Stop);

            Assert.That(!Svc.IsStarted);
            Assert.That(!Svc.IsListening);

            Assert.DoesNotThrow(() => Svc.Start());

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
            using var svc = new WebServiceBuilder { Url = "invalid" }.Build();
            Assert.Throws<ArgumentException>(svc.Start);
        }

        [Test]
        public async Task Service_ShouldReturnHttpNoContentByDefault() 
        {
            Svc = new WebServiceBuilder { Url = TestUrl }.Build();
            Svc.Start();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await response.Content.ReadAsStreamAsync()).Length, Is.EqualTo(0));
        }

        [Test]
        public async Task Service_ShouldHandlePreflightRequests()
        {
            const string origin = "http://cica.hu";

            var mockProcessor = new Mock<Func<RequestContext, Task>>(MockBehavior.Strict);

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
            Svc.Start();

            using var client = new HttpClient();

            var req = new HttpRequestMessage(HttpMethod.Options, TestUrl);
            req.Headers.Add("Origin", origin);

            HttpResponseMessage response = await client.SendAsync(req);

            Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin").Single(), Is.EqualTo(origin));
            Assert.That(response.Headers.GetValues("Vary").Single(), Is.EqualTo("Origin"));
            
            mockProcessor.Verify(ctx => ctx(It.IsAny<RequestContext>()), Times.Never);
        }

        private async Task Service_ShouldAllowAnyXxXByDefault(string headerName) 
        {
            var mockProcessor = new Mock<Func<RequestContext, Task>>(MockBehavior.Strict);

            Svc = CreateService(conf => (conf as RequestDelegator)?.SetHandler(mockProcessor.Object));
            Svc.Start();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, TestUrl));

            Assert.That(response.Headers.GetValues(headerName).Single(), Is.EqualTo("*"));

            mockProcessor.Verify(ctx => ctx(It.IsAny<RequestContext>()), Times.Never);
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
                        delgator.SetHandler(ctx => processor = Task.Factory.StartNew(() =>
                        {
                            using ManualResetEventSlim evt = new();
                            evt.Wait(ctx.Cancellation);
                        }, TaskCreationOptions.LongRunning));
                        break;
                    case RequestTimeout timeout:
                        timeout.Timeout = TimeSpan.FromSeconds(1);
                        break;
                }
            });
            Svc.Start();

            using HttpClient client = new();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(new OperationCanceledException().Message));

            Thread.Sleep(100);
            Assert.That(processor.IsCompleted);
        }

        [Test]
        public async Task Service_ShouldRejectTheRequestIfThereIsAThresholSet()
        {
            Svc = 
                CreateBuilder(conf => 
                {
                    switch (conf)
                    {
                        case RequestLimiter requestLimiter:
                            requestLimiter.Interval = () => TimeSpan.FromSeconds(2);
                            requestLimiter.Threshold = () => 1;
                            break;
                    }
                })
                .Build();
            Svc.Start();

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
