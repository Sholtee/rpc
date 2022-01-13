/********************************************************************************
* RequestLimiter.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
    using Interfaces;
    using Proxy.Generators;

    [TestFixture]
    public class RequestLimiterTests
    {
        [RequestLimiterAspect(threshold: 10)]
        public interface IModule
        {
            [RequestThreshold(1)]
            void DoSomething();
            [RequestThreshold(1)]
            Task DoSomethingAsync();
            int Property
            {
                [RequestThreshold("my_variable")]
                get;
            }
        }

        [Test]
        public void RequestLimiter_ShouldThrowIfTheThresholIsReached()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomething());

            var mockContext = new Mock<IRpcRequestContext>(MockBehavior.Strict);
            mockContext
                .SetupGet(ctx => ctx.RemoteEndPoint)
                .Returns(new IPEndPoint(IPAddress.Loopback, 1986));
            mockContext
                .SetupGet(ctx => ctx.Module)
                .Returns(nameof(IModule));
            mockContext
                .SetupGet(ctx => ctx.Method)
                .Returns(nameof(IModule.DoSomething));

            DateTime now = DateTime.UtcNow;

            const string requestId = "127.0.0.1:1986_IModule_DoSomething";

            var mockClock = new Mock<IClock>(MockBehavior.Strict);
            mockClock
                .SetupGet(c => c.UtcNow)
                .Returns(now);

            int invocationCount = 0;

            var mockCounter = new Mock<IRequestCounter>(MockBehavior.Strict);
            mockCounter
                .Setup(rq => rq.RegisterRequest(requestId, now))
                .Callback<string, DateTime>((_, _) => invocationCount++);
            mockCounter
                .Setup(rq => rq.CountRequest(requestId, now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now))
                .Returns<string, DateTime, DateTime>((_, _, _) => invocationCount);

            Type proxyType = ProxyGenerator<IModule, RequestLimiter<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockContext.Object, mockCounter.Object, mockClock.Object);

            Assert.DoesNotThrow(module.DoSomething);
            HttpException ex = Assert.Throws<HttpException>(module.DoSomething);
            Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.Forbidden));

            mockCounter.Verify(c => c.RegisterRequest(requestId, now), Times.Exactly(2));
            mockCounter.Verify(c => c.CountRequest(requestId, now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now), Times.Exactly(2));
        }

        [Test]
        public void RequestLimiterAsync_ShouldThrowIfTheThresholIsReached()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomethingAsync())
                .Returns(Task.CompletedTask);

            var mockContext = new Mock<IRpcRequestContext>(MockBehavior.Strict);
            mockContext
                .SetupGet(ctx => ctx.RemoteEndPoint)
                .Returns(new IPEndPoint(IPAddress.Loopback, 1986));
            mockContext
                .SetupGet(ctx => ctx.Module)
                .Returns(nameof(IModule));
            mockContext
                .SetupGet(ctx => ctx.Method)
                .Returns(nameof(IModule.DoSomethingAsync));
            mockContext
                .SetupGet(ctx => ctx.Cancellation)
                .Returns(default(CancellationToken));

            DateTime now = DateTime.UtcNow;

            const string requestId = "127.0.0.1:1986_IModule_DoSomethingAsync";

            var mockClock = new Mock<IClock>(MockBehavior.Strict);
            mockClock
                .SetupGet(c => c.UtcNow)
                .Returns(now);

            int invocationCount = 0;

            var mockCounter = new Mock<IRequestCounter>(MockBehavior.Strict);
            mockCounter
                .Setup(rq => rq.RegisterRequestAsync(requestId, now, default))
                .Returns<string, DateTime, CancellationToken>((_, _, _) => { invocationCount++; return Task.CompletedTask; });
            mockCounter
                .Setup(rq => rq.CountRequestAsync(requestId, now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now, default))
                .Returns<string, DateTime, DateTime, CancellationToken>((_, _, _, _) => Task.FromResult(invocationCount));

            Type proxyType = ProxyGenerator<IModule, RequestLimiter<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockContext.Object, mockCounter.Object, mockClock.Object);

            Assert.DoesNotThrowAsync(module.DoSomethingAsync);
            HttpException ex = Assert.ThrowsAsync<HttpException>(module.DoSomethingAsync);
            Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.Forbidden));

            mockCounter.Verify(c => c.RegisterRequestAsync(requestId, now, default), Times.Exactly(2));
            mockCounter.Verify(c => c.CountRequestAsync(requestId, now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now, default), Times.Exactly(2));
        }

        [Test]
        public void RequestLimiter_ShouldDistinguishBetweenRemoteEndpoints()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomething());

            IPEndPoint remoteEndPoint = new(IPAddress.Loopback, 1986);

            var mockContext = new Mock<IRpcRequestContext>(MockBehavior.Strict);
            mockContext
                .SetupGet(ctx => ctx.RemoteEndPoint)
                .Returns(() => remoteEndPoint);
            mockContext
                .SetupGet(ctx => ctx.Module)
                .Returns(nameof(IModule));
            mockContext
                .SetupGet(ctx => ctx.Method)
                .Returns(nameof(IModule.DoSomething));

            DateTime now = DateTime.UtcNow;

            var mockClock = new Mock<IClock>(MockBehavior.Strict);
            mockClock
                .SetupGet(c => c.UtcNow)
                .Returns(now);

            Dictionary<string, int> requests = new()
            {
                ["127.0.0.1:1986_IModule_DoSomething"] = 0,
                ["127.0.0.1:1987_IModule_DoSomething"] = 0
            };

            var mockCounter = new Mock<IRequestCounter>(MockBehavior.Strict);
            mockCounter
                .Setup(rq => rq.RegisterRequest(It.Is<string>(id => requests.ContainsKey(id)), now));
            mockCounter
                .Setup(rq => rq.CountRequest(It.Is<string>(id => requests.ContainsKey(id)), now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now))
                .Returns<string, DateTime, DateTime>((id, _, _) => requests[id]++);

            Type proxyType = ProxyGenerator<IModule, RequestLimiter<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockContext.Object, mockCounter.Object, mockClock.Object);

            Assert.DoesNotThrow(module.DoSomething);
            remoteEndPoint = new(IPAddress.Loopback, 1987);
            Assert.DoesNotThrow(module.DoSomething);

            mockCounter.Verify(c => c.RegisterRequest("127.0.0.1:1986_IModule_DoSomething", now), Times.Once);
            mockCounter.Verify(c => c.CountRequest("127.0.0.1:1986_IModule_DoSomething", now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now), Times.Once);
            mockCounter.Verify(c => c.RegisterRequest("127.0.0.1:1987_IModule_DoSomething", now), Times.Once);
            mockCounter.Verify(c => c.CountRequest("127.0.0.1:1987_IModule_DoSomething", now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now), Times.Once);
        }

        [Test]
        public void RequestLimiterAsync_ShouldDistinguishBetweenRemoteEndpoints()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomethingAsync())
                .Returns(Task.CompletedTask);

            IPEndPoint remoteEndPoint = new(IPAddress.Loopback, 1986);

            var mockContext = new Mock<IRpcRequestContext>(MockBehavior.Strict);
            mockContext
                .SetupGet(ctx => ctx.RemoteEndPoint)
                .Returns(() => remoteEndPoint);
            mockContext
                .SetupGet(ctx => ctx.Module)
                .Returns(nameof(IModule));
            mockContext
                .SetupGet(ctx => ctx.Method)
                .Returns(nameof(IModule.DoSomething));
            mockContext
                .SetupGet(ctx => ctx.Cancellation)
                .Returns(default(CancellationToken));

            DateTime now = DateTime.UtcNow;

            var mockClock = new Mock<IClock>(MockBehavior.Strict);
            mockClock
                .SetupGet(c => c.UtcNow)
                .Returns(now);

            Dictionary<string, int> requests = new()
            {
                ["127.0.0.1:1986_IModule_DoSomething"] = 0,
                ["127.0.0.1:1987_IModule_DoSomething"] = 0
            };

            var mockCounter = new Mock<IRequestCounter>(MockBehavior.Strict);
            mockCounter
                .Setup(rq => rq.RegisterRequestAsync(It.Is<string>(id => requests.ContainsKey(id)), now, default))
                .Returns(Task.CompletedTask);
            mockCounter
                .Setup(rq => rq.CountRequestAsync(It.Is<string>(id => requests.ContainsKey(id)), now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now, default))
                .Returns<string, DateTime, DateTime, CancellationToken>((id, _, _, _) => Task.FromResult(requests[id]++));

            Type proxyType = ProxyGenerator<IModule, RequestLimiter<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockContext.Object, mockCounter.Object, mockClock.Object);

            Assert.DoesNotThrowAsync(module.DoSomethingAsync);
            remoteEndPoint = new(IPAddress.Loopback, 1987);
            Assert.DoesNotThrowAsync(module.DoSomethingAsync);

            mockCounter.Verify(c => c.RegisterRequestAsync("127.0.0.1:1986_IModule_DoSomething", now, default), Times.Once);
            mockCounter.Verify(c => c.CountRequestAsync("127.0.0.1:1986_IModule_DoSomething", now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now, default), Times.Once);
            mockCounter.Verify(c => c.RegisterRequestAsync("127.0.0.1:1987_IModule_DoSomething", now, default), Times.Once);
            mockCounter.Verify(c => c.CountRequestAsync("127.0.0.1:1987_IModule_DoSomething", now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now, default), Times.Once);
        }

        [Test]
        public void THreshold_MayBeChangedRuntime()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .SetupGet(m => m.Property)
                .Returns(0);

            var mockContext = new Mock<IRpcRequestContext>(MockBehavior.Strict);
            mockContext
                .SetupGet(ctx => ctx.RemoteEndPoint)
                .Returns(new IPEndPoint(IPAddress.Loopback, 1986));
            mockContext
                .SetupGet(ctx => ctx.Module)
                .Returns(nameof(IModule));
            mockContext
                .SetupGet(ctx => ctx.Method)
                .Returns("get_Property");

            DateTime now = DateTime.UtcNow;

            const string requestId = "127.0.0.1:1986_IModule_get_Property";

            var mockClock = new Mock<IClock>(MockBehavior.Strict);
            mockClock
                .SetupGet(c => c.UtcNow)
                .Returns(now);

            int invocationCount = 0;

            var mockCounter = new Mock<IRequestCounter>(MockBehavior.Strict);
            mockCounter
                .Setup(rq => rq.RegisterRequest(requestId, now))
                .Callback<string, DateTime>((_, _) => invocationCount++);
            mockCounter
                .Setup(rq => rq.CountRequest(requestId, now.Subtract(TimeSpan.FromMilliseconds(RequestLimiterAspectAttribute.DEFAULT_INTERVAL)), now))
                .Returns<string, DateTime, DateTime>((_, _, _) => invocationCount);

            Type proxyType = ProxyGenerator<IModule, RequestLimiter<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockContext.Object, mockCounter.Object, mockClock.Object);

            Environment.SetEnvironmentVariable("my_variable", 1.ToString());

            Assert.DoesNotThrow(() => _ = module.Property);
            HttpException ex = Assert.Throws<HttpException>(() => _ = module.Property);
            Assert.That(ex.Status, Is.EqualTo(HttpStatusCode.Forbidden));

            Environment.SetEnvironmentVariable("my_variable", 3.ToString());
            Assert.DoesNotThrow(() => _ = module.Property);
        }
    }
}
