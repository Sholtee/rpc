/********************************************************************************
* RequestLimiterHandler.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1033 // Interface methods should be callable by child types

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// Specifies the <see cref="RequestLimiterHandler"/> configuration.
    /// </summary>
    public interface IRequestLimiterHandlerConfig
    {
        /// <summary>
        /// Determines the interval between two request counter resets.
        /// </summary>
        /// <remarks>This property may change in runtime.</remarks>
        TimeSpan Interval { get; }

        /// <summary>
        /// The maximum amount of requests allowed for a remote client (under a given <see cref="Interval"/>).
        /// </summary>
        /// <remarks>This property may change in runtime.</remarks>
        int Threshold { get; }
    }

    /// <summary>
    /// Rejects the request if the request count (made by a remote client) excceeds the threshold.
    /// </summary>
    public class RequestLimiterHandler : RequestHandlerBase<IRequestLimiterHandlerConfig>
    {
        private static readonly MemoryCache FCache = new(nameof(RequestLimiter));

        private sealed class Box { public int Count; }

        internal void ThrowIfRequestCountExceedsTheThreshold(IPEndPoint remoteEndPoint, DateTime nowUtc) // tesztekhez van kulon
        {
            Box 
                @new = new(),
                counter = (Box) FCache.AddOrGetExisting(remoteEndPoint.ToString(), @new, nowUtc + Config.Interval) ?? @new;

            if (Interlocked.Increment(ref counter.Count) > Config.Threshold)
                throw new HttpException { Status = HttpStatusCode.Forbidden };
        }

        /// <summary>
        /// Creates a new <see cref="RequestLimiterHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public RequestLimiterHandler(IRequestHandler next, IRequestLimiterHandlerConfig config): base(next, config) { }

        /// <inheritdoc/>
        public override Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            ThrowIfRequestCountExceedsTheThreshold(context.Request.RemoteEndPoint, DateTime.UtcNow);
            return Next.HandleAsync(scope, context, cancellation);
        }
    }

    /// <summary>
    /// Limits how many times a remote client can access local resources (in a given period of time).
    /// </summary>
    public class RequestLimiter : RequestHandlerBuilder, IRequestLimiterHandlerConfig
    {
        private Func<TimeSpan> FGetInterval = () => TimeSpan.FromSeconds(10);

        private Func<int> FGetThreshold = () => 1000;

        /// <summary>
        /// Creates a new <see cref="RequestLimiter"/> instance.
        /// </summary>
        public RequestLimiter(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent) : base(webServiceBuilder, parent) { }

        /// <summary>
        /// Sets a function to be used to get the <see cref="IRequestLimiterHandlerConfig.Interval"/> value.
        /// </summary>
        public RequestLimiter SetDynamicInterval(Func<TimeSpan> getter)
        {
            FGetInterval = getter ?? throw new ArgumentNullException(nameof(getter));
            return this;
        }

        /// <summary>
        /// Setsthe <see cref="IRequestLimiterHandlerConfig.Interval"/> value.
        /// </summary>
        public RequestLimiter SetStaticInterval(TimeSpan interval) => SetDynamicInterval(() => interval);

        /// <summary>
        /// Sets a function to be used to get the <see cref="IRequestLimiterHandlerConfig.Threshold"/> value.
        /// </summary>
        public RequestLimiter SetDynamicThreshold(Func<int> getter)
        {
            FGetThreshold = getter ?? throw new ArgumentNullException(nameof(getter));
            return this;
        }

        /// <summary>
        /// Sets the <see cref="IRequestLimiterHandlerConfig.Threshold"/> value.
        /// </summary>
        public RequestLimiter SetStaticThreshold(int threshold) => SetDynamicThreshold(() => threshold);

        /// <summary>
        /// Creates a new <see cref="RequestLimiterHandler"/> instance.
        /// </summary>
        public override IRequestHandler Build(IRequestHandler next) => new RequestLimiterHandler(next, this);

        TimeSpan IRequestLimiterHandlerConfig.Interval => FGetInterval();

        int IRequestLimiterHandlerConfig.Threshold => FGetThreshold();
    }
}
