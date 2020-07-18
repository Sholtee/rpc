﻿/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using Internals;

    [TestFixture]
    public class WebServiceTests
    {
        const string 
            Hello   = "Hello World",
            TestUrl = "http://127.0.0.1:1986/test/";

        private class DummyWebService : WebService 
        {
            protected override Task ProcessRequestContext(HttpListenerContext context)
            {
                if (OnRequest != null)
                {
                    OnRequest.Invoke(context);
                    return Task.CompletedTask;
                }
                return base.ProcessRequestContext(context);
            }

            public Action<HttpListenerContext> OnRequest { get; set; }

            public DummyWebService() => OnRequest = context =>
            {
                HttpListenerResponse response = context.Response;

                response.ContentType = "text/html";
                response.StatusCode = 200;

                WriteResponseString(response, Hello).Wait();

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

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await response.Content.ReadAsStreamAsync()).Length, Is.EqualTo(0));
        }
    }
}