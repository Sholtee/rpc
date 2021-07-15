/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI.Interfaces;
    using Internals;   

    [TestFixture]
    public class WebServiceTests
    {
        const string 
            Hello   = "Hello World",
            TestUrl = "http://127.0.0.1:1986/test/";

        private class DummyWebService : WebService 
        {
            protected override Task Process(HttpListenerContext context, IInjector injector, CancellationToken cancellation)
            {
                if (OnRequest != null)
                {
                    return OnRequest.Invoke(context, cancellation);
                }

                return base.Process(context, injector, cancellation);
            }

            public Func<HttpListenerContext, CancellationToken, Task> OnRequest { get; set; }

            public DummyWebService() => OnRequest = (context, _) =>
            {
                HttpListenerResponse response = context.Response;

                response.ContentType = "text/html";
                response.StatusCode = 200;

                WriteResponseString(response, Hello).Wait();

                response.Close();

                return Task.CompletedTask;
            };
        }

        private DummyWebService Svc { get; set; }

        [SetUp]
        public void SetupFixture() 
        {
            Svc = new DummyWebService();
            Svc.Start(TestUrl);
        }

        [TearDown]
        public void TeardownFixture() 
        {
            Svc?.Dispose();
            Svc = null;
        }

        [Test]
        public async Task Service_ShouldHandleRequests() 
        {
            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.That(responseBody, Is.EqualTo(Hello));
        }

        [Test]
        public void Service_ShouldHandleRequestsAsynchronously() =>
            Assert.DoesNotThrowAsync(() => Task.WhenAll(Enumerable
                .Repeat<Func<Task>>(Service_ShouldHandleRequests, 100)
                .Select(_ => _())));

        [Test]
        public async Task Service_ShouldHandleExceptions() 
        {
            Svc.OnRequest = (_, __) => throw new Exception();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.That(responseBody, Is.EqualTo(new Exception().Message));
        }

        [Test]
        public async Task Service_CanBeRestarted() 
        {
            Assert.That(Svc.IsStarted);
            Assert.That(Svc.IsListening);

            Assert.DoesNotThrow(Svc.Stop);

            Assert.That(!Svc.IsStarted);
            Assert.That(!Svc.IsListening);

            Assert.DoesNotThrow(() => Svc.Start(TestUrl));

            Assert.That(Svc.IsStarted);
            Assert.That(Svc.IsListening);

            //
            // ujrainditas utan is mukodik
            //

            await Service_ShouldHandleRequests();
        }

        [Test]
        public void Start_ShouldValidateTheUrl() 
        {
            using var svc = new WebService();
            Assert.Throws<ArgumentException>(() => svc.Start("invalid"));
        }

        [Test]
        public async Task Service_ShouldReturnHttp200ByDefault() 
        {
            Svc.OnRequest = null;

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await response.Content.ReadAsStreamAsync()).Length, Is.EqualTo(0));
        }

        [Test]
        public async Task Service_ShouldHandlePreflightRequests()
        {
            const string origin = "http://cica.hu";

            var mockProcessor = new Mock<Func<HttpListenerContext, CancellationToken, Task>>(MockBehavior.Strict);

            Svc.OnRequest = mockProcessor.Object;
            Svc.AllowedOrigins.Add(origin);

            using var client = new HttpClient();

            var req = new HttpRequestMessage(HttpMethod.Options, TestUrl);
            req.Headers.Add("Origin", origin);

            HttpResponseMessage response = await client.SendAsync(req);

            Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin").Single(), Is.EqualTo(origin));
            Assert.That(response.Headers.GetValues("Vary").Single(), Is.EqualTo("Origin"));
            
            mockProcessor.Verify(ctx => ctx(It.IsAny<HttpListenerContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private async Task Service_ShouldAllowAnyXxXByDefault(string headerName) 
        {
            var mockProcessor = new Mock<Func<HttpListenerContext, CancellationToken, Task>>(MockBehavior.Strict);

            Svc.OnRequest = mockProcessor.Object;

            using var client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, TestUrl));

            Assert.That(response.Headers.GetValues(headerName).Single(), Is.EqualTo("*"));

            mockProcessor.Verify(ctx => ctx(It.IsAny<HttpListenerContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Service_ShouldAllowAnyHeaderByDefault() => await Service_ShouldAllowAnyXxXByDefault("Access-Control-Allow-Headers");

        [Test]
        public async Task Service_ShouldAllowAnyMethodByDefault() => await Service_ShouldAllowAnyXxXByDefault("Access-Control-Allow-Methods");

        [Test]
        public async Task Service_ShouldTimeout()
        {
            Task processor = null;

            Svc.OnRequest = (context, cancellation) => processor = Task.Factory.StartNew(() => 
            {
                using var evt = new ManualResetEventSlim();
                evt.Wait(cancellation);
            }, TaskCreationOptions.LongRunning);
            Svc.Timeout = TimeSpan.FromSeconds(1);

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(new OperationCanceledException().Message));

            Thread.Sleep(100);
            Assert.That(processor.IsCompleted);
        }
    }
}
