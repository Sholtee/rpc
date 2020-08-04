/********************************************************************************
* RequestContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace Solti.Utils.Rpc.Internals
{
    using Properties;

    internal class RequestContext : IRequestContext
    {
        internal RequestContext(string? sessionId, string module, string method, Stream payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellation)
        {
            SessionId    = sessionId;
            Module       = module;
            Method       = method;
            Payload      = payload;
            Cancellation = cancellation;
            Headers      = headers;
        }

        public RequestContext(HttpListenerRequest request, CancellationToken cancellation): this
        (
            request.QueryString.Get("sessionid"),
            request.QueryString.Get("module") ?? throw new InvalidOperationException(Resources.NO_MODULE),
            request.QueryString.Get("method") ?? throw new InvalidOperationException(Resources.NO_METHOD),
            request.InputStream,
            request.Headers.AllKeys.ToDictionary(key => key, key => request.Headers[key]),
            cancellation
        ) {}

        public string? SessionId { get; }

        public string Module { get; }

        public string Method { get; }

        public Stream Payload { get; }

        public CancellationToken Cancellation { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
