/********************************************************************************
* RequestLimiterAspectAttribute.cs                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

#pragma warning disable CA1707 // Identifiers should not contain underscores

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Annotates a module to be intercepted by a request limiter that defines a request threashold on every method.
    /// </summary>
    /// <remarks>The threshold limits the number of requests to be served in the direction of a remote endpoint.</remarks>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class RequestLimiterAspectAttribute: AspectAttribute
    {
        /// <summary>
        /// Default threshold.
        /// </summary>
        public const int DEFAULT_THRESHOLD = 1000;

        /// <summary>
        /// Default interval.
        /// </summary>
        public const int DEFAULT_INTERVAL = 10000;

        /// <summary>
        /// The default threshold (allowed requests).
        /// </summary>
        /// <remarks>This value can be overridden by the <see cref="RequestThresholdAttribute"/>.</remarks>
        public int Threshold { get; }

        /// <summary>
        /// The interval (in milliseconds) on which the <see cref="Threshold"/> is applied.
        /// </summary>
        public int Interval { get; }

        /// <summary>
        /// Creates a new <see cref="RequestLimiterAspectAttribute"/> instance.
        /// </summary>
        public RequestLimiterAspectAttribute(int threshold = DEFAULT_THRESHOLD, int interval = DEFAULT_INTERVAL)
        {
            Threshold = threshold;
            Interval = interval;
        }

        /// <inheritdoc/>
        public override Type GetInterceptorType(Type iface)
        {
            if (iface is null)
                throw new ArgumentNullException(nameof(iface));

            //
            // Rpc.Server szerelveny verzioja megegyezik az Rpc.Interfaces szerelveny verziojaval
            //

            Type interceptor = Type.GetType($"Solti.Utils.Rpc.Aspects.RequestLimiter`1, Solti.Utils.Rpc.Server, Version = {GetType().Assembly.GetName().Version}, Culture = neutral, PublicKeyToken = null", throwOnError: true);
            return interceptor.MakeGenericType(iface);
        }
    }
}
