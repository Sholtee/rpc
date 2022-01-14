/********************************************************************************
* AccessControlHandler.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Handles access control HTTP requests.
    /// </summary>
    public class HttpAccessControlHandler : IRequestHandler
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

            response.Headers["Access-Control-Allow-Methods"] = string.Join(", ", AllowedMethods);
            response.Headers["Access-Control-Allow-Headers"] = string.Join(", ", AllowedHeaders);
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
        public IReadOnlyCollection<string> AllowedOrigins { get; }

        /// <summary>
        /// Allowed methods.
        /// </summary>
        public IReadOnlyCollection<string> AllowedMethods { get; }

        /// <summary>
        /// Allowed headers.
        /// </summary>
        public IReadOnlyCollection<string> AllowedHeaders { get; }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// Creates a new <see cref="HttpAccessControlHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public HttpAccessControlHandler(IRequestHandler next, IReadOnlyCollection<string> allowedOrigins, IReadOnlyCollection<string> allowedMethods, IReadOnlyCollection<string> allowedHeaders)
        {
            Next = next ?? throw new ArgumentNullException(nameof(next));
            AllowedOrigins = allowedOrigins ?? throw new ArgumentNullException(nameof(allowedOrigins));
            AllowedMethods = allowedMethods ?? throw new ArgumentNullException(nameof(allowedMethods));
            AllowedHeaders = allowedHeaders ?? throw new ArgumentNullException(nameof(allowedHeaders));
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

    /// <summary>
    /// Handles access control HTTP requests.
    /// </summary>
    public class HttpAccessControl : RequestHandlerFactory
    {
        /// <summary>
        /// The allowed origins. See https://en.wikipedia.org/wiki/Cross-origin_resource_sharing
        /// </summary>
        public ICollection<string> AllowedOrigins { get; } = new HashSet<string>();

        /// <summary>
        /// Allowed methods.
        /// </summary>
        public ICollection<string> AllowedMethods { get; } = new HashSet<string>();

        /// <summary>
        /// Allowed headers.
        /// </summary>
        public ICollection<string> AllowedHeaders { get; } = new HashSet<string>();

        /// <inheritdoc/>
        public override IRequestHandler Create(IRequestHandler next) => new HttpAccessControlHandler
        (
            next,
            (IReadOnlyCollection<string>) AllowedOrigins,
            (IReadOnlyCollection<string>) (AllowedMethods.Count == 0 ? new string[] { "*" } : AllowedMethods),
            (IReadOnlyCollection<string>) (AllowedHeaders.Count == 0 ? new string[] { "*" } : AllowedHeaders)
        );
    }
}
