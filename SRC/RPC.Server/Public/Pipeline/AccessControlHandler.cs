/********************************************************************************
* AccessControlHandler.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Properties;

    /// <summary>
    /// Handles access control requests.
    /// </summary>
    public class AccessControlHandler : IRequestHandler
    {
        /// <summary>
        /// Sets the "Access-Control-XxX" headers.
        /// </summary>
        /// <remarks>This method may be called parallelly.</remarks>
        protected virtual void SetAcHeaders(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            HttpListenerResponse response = context.Response;

            string? origin = context.Request.Headers.Get("Origin");

            if (!string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin))
            {
                response.Headers["Access-Control-Allow-Origin"] = origin;
                response.Headers["Vary"] = "Origin";
            }

            response.Headers["Access-Control-Allow-Methods"] = "*";
            response.Headers["Access-Control-Allow-Headers"] = "*";
        }

        /// <summary>
        /// Determines whether the request is a preflight request or not.
        /// </summary>
        protected static bool IsPreflight(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return context
                .Request
                .HttpMethod
                .Equals(HttpMethod.Options.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The allowed origins. See https://en.wikipedia.org/wiki/Cross-origin_resource_sharing
        /// </summary>
        public ICollection<string> AllowedOrigins { get; }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// Creates a new <see cref="AccessControlHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public AccessControlHandler(IRequestHandler next, params string[] allowedOrigins)
        {
            Next = next ?? throw new ArgumentNullException(nameof(next));
            AllowedOrigins = allowedOrigins ?? throw new ArgumentNullException(nameof(allowedOrigins));
        }

        /// <inheritdoc/>
        public Task Handle(RequestContext context)
        {
            SetAcHeaders(context);

            if (IsPreflight(context))
            {
                context.Scope.TryGet<ILogger>()?.LogInformation(Trace.PREFLIGHT_REQUEST);
                context.Response.Close();
                return Task.CompletedTask;
            }

            return Next.Handle(context);
        }
    }
}
