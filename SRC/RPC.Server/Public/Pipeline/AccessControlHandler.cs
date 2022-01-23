/********************************************************************************
* AccessControlHandler.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
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
        private static readonly string[] AllowAll = new string[] { "*" };

        /// <summary>
        /// Sets the "Access-Control-XxX" headers.
        /// </summary>
        protected virtual void SetAcHeaders(IHttpSession context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            IHttpResponse response = context.Response;

            string? origin = context.Request.Headers["Origin"];

            //
            // Ez itt trukkos mert bar elmeletben itt is visszaadhatnank "*"-t a kliens fele gyakorlatban pl a Chrome
            // tuti nem eszi meg.
            //

            if (!string.IsNullOrEmpty(origin) && (AllowedOrigins == AllowAll || AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
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
        protected static bool IsPreflight(IHttpSession context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return context
                .Request
                .Method
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

        /// <summary>
        /// Returns true if the logging is allowed.
        /// </summary>
        public bool AllowLogs { get; }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// Creates a new <see cref="HttpAccessControlHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public HttpAccessControlHandler(IRequestHandler next, HttpAccessControl parent)
        {
            if (parent is null)
                throw new ArgumentNullException(nameof(parent));

            Next = next ?? throw new ArgumentNullException(nameof(next));

            AllowedOrigins = parent.AllowedOrigins.Count is 0 ? AllowAll : new List<string>(parent.AllowedOrigins);
            AllowedMethods = parent.AllowedMethods.Count is 0 ? AllowAll : new List<string>(parent.AllowedMethods);
            AllowedHeaders = parent.AllowedHeaders.Count is 0 ? AllowAll : new List<string>(parent.AllowedHeaders);

            AllowLogs = parent.AllowLogs;
        }

        /// <inheritdoc/>
        public Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (context is null)
                throw new ArgumentNullException(nameof(context));

            SetAcHeaders(context);

            if (IsPreflight(context))
            {
                if (AllowLogs)
                    scope.TryGet<ILogger>()?.LogInformation(Trace.PREFLIGHT_REQUEST);

                context.Response.Close();
                return Task.CompletedTask;
            }

            return Next.HandleAsync(scope, context, cancellation);
        }
    }

    /// <summary>
    /// Specifies how to handle access control HTTP requests.
    /// </summary>
    public class HttpAccessControl : RequestHandlerFactory, ISupportsLog
    {
        /// <summary>
        /// The allowed origins. See https://en.wikipedia.org/wiki/Cross-origin_resource_sharing
        /// </summary>
        /// <remarks>If this list is empty, all origins are allowed.</remarks>
        public ICollection<string> AllowedOrigins { get; } = new HashSet<string>();

        /// <summary>
        /// Allowed methods.
        /// </summary>
        /// <remarks>If this list is empty, all kind of methods are allowed.</remarks>
        public virtual ICollection<string> AllowedMethods { get; } = new HashSet<string>();

        /// <summary>
        /// Allowed headers.
        /// </summary>
        /// <remarks>If this list is empty, all kind of headers are allowed.</remarks>
        public virtual ICollection<string> AllowedHeaders { get; } = new HashSet<string>();

        /// <summary>
        /// Returns true if the logging is enabled.
        /// </summary>
        public bool AllowLogs { get; set; } = true;

        /// <inheritdoc/>
        protected override IRequestHandler Create(IRequestHandler next) => new HttpAccessControlHandler(next, this);
    }

    /// <summary>
    /// Specifies how to handle access control HTTP requests (RPC specific).
    /// </summary>
    /// <remarks>Allowed method: POST, Allowed headers: Content-Type, Content-Length</remarks>
    public class RpcAccessControl : HttpAccessControl
    {
        /// <summary>
        /// Allows POST method only.
        /// </summary>
        /// <remarks>This list is read-only.</remarks>
        public override ICollection<string> AllowedMethods { get; } = new string[] { "POST" };

        /// <summary>
        /// Allows Content-Type, Content-Length headers only.
        /// </summary>
        /// <remarks>This list is read-only.</remarks>
        public override ICollection<string> AllowedHeaders { get; } = new string[] { "Content-Type", "Content-Length" };
    }
}
