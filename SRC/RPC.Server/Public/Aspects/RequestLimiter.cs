/********************************************************************************
* RequestLimiter.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Proxy;

    /// <summary>
    /// Limits how many requests can be serverd in a given period of time. Every remote endpoint has its own limiter.
    /// </summary>
    /// <remarks>This interceptor should be applied against module interfaces only. In order to use this interceptor you should implement the <see cref="IRequestCounter"/> service.</remarks>
    public class RequestLimiter<TInterface> : AspectInterceptor<TInterface> where TInterface : class
    {
        private static RequestLimiterAspectAttribute? RelatedAspect { get; } = typeof(TInterface).GetCustomAttribute<RequestLimiterAspectAttribute>();

        /// <summary>
        /// The default threshold (allowed requests).
        /// </summary>
        public int Threshold { get; }

        /// <summary>
        /// The interval on which the <see cref="Threshold"/> is applied.
        /// </summary>
        public int Interval { get; }

        /// <summary>
        /// The request idintifier.
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// The request descriptor.
        /// </summary>
        public IRequestContext RequestContext { get; }

        /// <summary>
        /// The request counter service.
        /// </summary>
        public IRequestCounter RequestCounter { get; }

        /// <summary>
        /// Creates a new <see cref="RequestLimiter{TInterface}"/> instance.
        /// </summary>
        public RequestLimiter(TInterface target, IRequestContext requestContext, IRequestCounter requestCounter, int threshold, int interval) : base(target ?? throw new ArgumentNullException(nameof(target)))
        {
            Threshold = threshold;
            Interval = interval;
            RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
            RequestCounter = requestCounter ?? throw new ArgumentNullException(nameof(requestCounter));
            RequestId = $"{requestContext.OriginalRequest.RemoteEndPoint}_{requestContext.Module}_{requestContext.Method}";
        }

        /// <summary>
        /// Creates a new <see cref="RequestLimiter{TInterface}"/> instance.
        /// </summary>
        [ServiceActivator]
        public RequestLimiter(TInterface target, IRequestContext requestContext, IRequestCounter requestCounter) : this(
            target, 
            requestContext,
            requestCounter,
            RelatedAspect?.Threshold ?? RequestLimiterAspectAttribute.DEFAULT_THRESHOLD,
            RelatedAspect?.Interval ?? RequestLimiterAspectAttribute.DEFAULT_INTERVAL)
        {
        }

        /// <inheritdoc/>
        protected override object? Decorator(InvocationContext context, Func<object?> callNext)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            DateTime nowUtc = DateTime.UtcNow;

            if (RequestCounter.CountRequest(RequestId, nowUtc.Subtract(TimeSpan.FromMilliseconds(Interval)), nowUtc) > Threshold)
                throw new HttpException { Status = HttpStatusCode.Forbidden };

            RequestCounter.RegisterRequest(RequestId, nowUtc);

            return callNext();
        }

        /// <inheritdoc/>
        protected override async Task<Task> DecoratorAsync(InvocationContext context, Func<Task> callNext)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            DateTime nowUtc = DateTime.UtcNow;

            if (await RequestCounter.CountRequestAsync(RequestId, nowUtc.Subtract(TimeSpan.FromMilliseconds(Interval)), nowUtc, RequestContext.Cancellation) > Threshold)
                throw new HttpException { Status = HttpStatusCode.Forbidden };

            await RequestCounter.RegisterRequestAsync(RequestId, nowUtc, RequestContext.Cancellation);

            return callNext();
        }
    }
}
