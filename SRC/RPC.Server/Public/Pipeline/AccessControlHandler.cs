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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Properties;

    /// <summary>
    /// Specifies the <see cref="HttpAccessControlHandler"/> configuration.
    /// </summary>
    public interface IHttpAccessControlHandlerConfig
    {
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
    }

    /// <summary>
    /// Handles access control HTTP requests.
    /// </summary>
    public class HttpAccessControlHandler : RequestHandlerBase<IHttpAccessControlHandlerConfig>
    {
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

            if (!string.IsNullOrEmpty(origin) && (Config.AllowedOrigins.Contains("*") || Config.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
            {
                response.Headers["Access-Control-Allow-Origin"] = origin;
                response.Headers["Vary"] = "Origin";
            }

            response.Headers["Access-Control-Allow-Methods"] = string.Join(", ", Config.AllowedMethods);
            response.Headers["Access-Control-Allow-Headers"] = string.Join(", ", Config.AllowedHeaders);
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
        /// Creates a new <see cref="HttpAccessControlHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public HttpAccessControlHandler(IRequestHandler next, IHttpAccessControlHandlerConfig config): base(next, config) { }

        /// <inheritdoc/>
        public override Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (context is null)
                throw new ArgumentNullException(nameof(context));

            SetAcHeaders(context);

            if (IsPreflight(context))
            {
                if (Config.AllowLogs)
                    scope.TryGet<ILogger>()?.LogInformation(Trace.PREFLIGHT_REQUEST);

                context.Response.StatusCode = HttpStatusCode.NoContent;
                context.Response.Close();
                return Task.CompletedTask;
            }

            return Next.HandleAsync(scope, context, cancellation);
        }
    }

    /// <summary>
    /// Specifies how to handle access control HTTP requests.
    /// </summary>
    public class HttpAccessControl : RequestHandlerBuilder, IHttpAccessControlHandlerConfig
    {
        /// <summary>
        /// Creates a new <see cref="HttpAccessControl"/> instance.
        /// </summary>
        public HttpAccessControl(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent) : base(webServiceBuilder, parent) { }

        /// <summary>
        /// The allowed origins. See https://en.wikipedia.org/wiki/Cross-origin_resource_sharing
        /// </summary>
        /// <remarks>If the collection is empty, all origins are allowed.</remarks>
        public ICollection<string> AllowedOrigins { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Allowed methods.
        /// </summary>
        /// <remarks>If the collection is empty, all kind of methods are allowed.</remarks>
        public virtual ICollection<string> AllowedMethods { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Allowed headers.
        /// </summary>
        /// <remarks>If the collection is empty, all kind of headers are allowed.</remarks>
        public virtual ICollection<string> AllowedHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override IRequestHandler Build(IRequestHandler next) => new HttpAccessControlHandler(next, this);

        /// <summary>
        /// Returns true if the logging is enabled.
        /// </summary>
        public bool AllowLogs { get; set; }

        private static readonly string[] AllowAll = new[] { "*" };

        private static IReadOnlyCollection<string> AllowAllIfEmpty(ICollection<string> coll) => coll.Count is 0
            ? AllowAll
            : coll as IReadOnlyCollection<string> ?? coll.ToArray();

        IReadOnlyCollection<string> IHttpAccessControlHandlerConfig.AllowedOrigins => AllowAllIfEmpty(AllowedOrigins);

        IReadOnlyCollection<string> IHttpAccessControlHandlerConfig.AllowedMethods => AllowAllIfEmpty(AllowedMethods);

        IReadOnlyCollection<string> IHttpAccessControlHandlerConfig.AllowedHeaders => AllowAllIfEmpty(AllowedHeaders);
    }

    /// <summary>
    /// Specifies how to handle access control HTTP requests (RPC specific).
    /// </summary>
    /// <remarks>Allowed method: POST, Allowed headers: Content-Type, Content-Length</remarks>
    public class RpcAccessControl : HttpAccessControl
    {
        /// <summary>
        /// Creates a new <see cref="RpcAccessControl"/> instance.
        /// </summary>
        public RpcAccessControl(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent) : base(webServiceBuilder, parent) { }

        /// <summary>
        /// Allows POST method only.
        /// </summary>
        /// <remarks>This list is read-only.</remarks>
        public override ICollection<string> AllowedMethods { get; } = new string[]
        { 
            //
            // Module invocation
            //

            "POST",
        
            //
            // Schema request
            //

            "GET"
        };

        /// <summary>
        /// Allows Content-Type, Content-Length headers only.
        /// </summary>
        /// <remarks>This list is read-only.</remarks>
        public override ICollection<string> AllowedHeaders { get; } = new string[] { "Content-Type", "Content-Length" };
    }
}
