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

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// Rejects the request if the request count (made by a remote client) excceeds the threshold.
    /// </summary>
    public class RequestLimiterHandler : IRequestHandler
    {
        private static readonly MemoryCache FCache = new(nameof(RequestLimiter));

        private sealed class Box { public int Count; }

        internal void ThrowIfRequestCountExceedsTheThreshold(IPEndPoint remoteEndPoint, DateTime nowUtc) // tesztekhez van kulon
        {
            Box 
                @new = new(),
                counter = (Box) FCache.AddOrGetExisting(remoteEndPoint.ToString(), @new, nowUtc + Parent.Interval()) ?? @new;

            if (Interlocked.Increment(ref counter.Count) > Parent.Threshold())
                throw new HttpException { Status = HttpStatusCode.Forbidden };
        }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// The parent instance.
        /// </summary>
        public RequestLimiter Parent { get; }

        /// <summary>
        /// Creates a new <see cref="RequestLimiterHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public RequestLimiterHandler(IRequestHandler next, RequestLimiter parent)
        {
            Next   = next   ?? throw new ArgumentNullException(nameof(next));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        /// <inheritdoc/>
        public Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
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
    public class RequestLimiter : RequestHandlerFactory
    {
        /// <summary>
        /// The interval on which the request counting takes palce.
        /// </summary>
        /// <remarks>This property is a func so the returned value can be changed in runtime.</remarks>
        public Func<TimeSpan> Interval { get; set; } = () => TimeSpan.FromSeconds(10);

        /// <summary>
        /// The request threshold.
        /// </summary>
        /// <remarks>This property is a func so the returned value can be changed in runtime.</remarks>
        public Func<int> Threshold { get; set; } = () => 1000;

        /// <summary>
        /// Creates a new <see cref="RequestLimiterHandler"/> instance.
        /// </summary>
        protected override IRequestHandler Create(IRequestHandler next) => new RequestLimiterHandler(next, this);
    }
}
