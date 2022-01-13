/********************************************************************************
* RequestLimiterHandler.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// Limits how many times a remote client could access the local resources (in a given period of time).
    /// </summary>
    public class RequestLimiterHandler : IRequestHandler
    {
        internal async Task ThrowIfRequestCountExceedsTheThreshold(IRequestCounter requestCounter, IPEndPoint remoteEndPoint, DateTime nowUtc, CancellationToken cancellation) // tesztekhez van kulon
        {
            string ipEp = remoteEndPoint.ToString();

            await requestCounter.RegisterRequestAsync(ipEp, nowUtc, cancellation);

            if (await requestCounter.CountRequestAsync(ipEp, nowUtc.Subtract(Interval()), nowUtc, cancellation) > Threshold())
                throw new HttpException { Status = HttpStatusCode.Forbidden };
        }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// The interval on which the request counting takes palce.
        /// </summary>
        /// <remarks>This property is a func so the returned value can be changed in runtime.</remarks>
        public Func<TimeSpan> Interval { get; }

        /// <summary>
        /// The request threshold.
        /// </summary>
        /// <remarks>This property is a func so the returned value can be changed in runtime.</remarks>
        public Func<int> Threshold { get; }

        /// <summary>
        /// Creates a new <see cref="RequestLimiterHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public RequestLimiterHandler(IRequestHandler next, Func<TimeSpan> interval, Func<int> threshold)
        {
            Next = next ?? throw new ArgumentNullException(nameof(next));
            Interval = interval ?? throw new ArgumentNullException(nameof(interval));
            Threshold = threshold ?? throw new ArgumentNullException(nameof(threshold));
        }

        /// <inheritdoc/>
        public async Task Handle(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            await ThrowIfRequestCountExceedsTheThreshold(context.Scope.Get<IRequestCounter>(), context.Request.RemoteEndPoint, DateTime.UtcNow, context.Cancellation);
            await Next.Handle(context);
        }
    }
}
