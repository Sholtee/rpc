/********************************************************************************
* WebServiceBase.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.AppHost.Tests
{
    using Internals;

    [TestFixture]
    public class WebServiceBaseTests
    {
        const string 
            Hello   = "Hello World",
            TestUrl = "http://127.0.0.1:1986/test/";

        private class DummyWebService : WebServiceBase 
        {
            protected override Task ProcessRequestContext(HttpListenerContext context)
            {
                OnRequest.Invoke(context);
                return Task.CompletedTask;
            }

            public Action<HttpListenerContext> OnRequest { get; set; }

            public DummyWebService() => OnRequest = context =>
            {
                HttpListenerResponse response = context.Response;

                response.ContentType = "text/html";
                response.StatusCode = 200;

                WriteResponseString(response, Hello);

                response.Close();
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
            Svc.Stop();
            Svc.Dispose();
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
        public void Service_ShouldHandleRequestsAsynchronously() 
        {
            Task[] tasks = Enumerable.Repeat(0, 100).Select(_ => Service_ShouldHandleRequests()).ToArray();

            Assert.DoesNotThrow(() => Task.WaitAll(tasks));
        }

        [Test]
        public async Task Service_ShouldHandleExceptions() 
        {
            Svc.OnRequest = _ => throw new Exception();

            using var client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(TestUrl);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            string responseBody = await response.Content.ReadAsStringAsync();

            Assert.That(responseBody, Is.EqualTo("Internal Server Error"));
        }

        [Test]
        public void Service_CanBeRestarted() 
        {
            Assert.That(Svc.IsStarted);
            Assert.That(Svc.IsListening);

            Assert.DoesNotThrow(Svc.Stop);

            Assert.That(!Svc.IsStarted);
            Assert.That(!Svc.IsListening);

            Assert.DoesNotThrow(() => Svc.Start(TestUrl));

            Assert.That(Svc.IsStarted);
            Assert.That(Svc.IsListening);
        }
    }
}
