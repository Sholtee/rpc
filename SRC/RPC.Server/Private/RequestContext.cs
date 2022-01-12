/********************************************************************************
* RequestContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;
    using Properties;

    internal sealed class RequestContext : IRequestContext
    {
        private readonly HttpListenerRequest FOriginalRequest;

#if DEBUG || PERF
        internal RequestContext(string? sessionId, string module, string method, IPEndPoint remoteEndPoint, Stream payload, CancellationToken cancellation)
        {
            SessionId      = sessionId;
            Module         = module;
            Method         = method;
            RemoteEndPoint = remoteEndPoint;
            Payload        = payload;
            Cancellation   = cancellation;

            FHeaders           = new Dictionary<string, string>();
            FRequestParameters = new Dictionary<string, string>();

            FOriginalRequest = null!;
        }
#endif
        public RequestContext(HttpListenerRequest request, CancellationToken cancellation)
        {
            NameValueCollection paramz = request.QueryString;

            //
            // Szukseges parameterek lekerdezese (nem kis-nagy betu erzekeny).
            //

            SessionId = paramz[nameof(SessionId)];
            Module    = paramz[nameof(Module)] ?? throw new InvalidOperationException(Errors.NO_MODULE);
            Method    = paramz[nameof(Method)] ?? throw new InvalidOperationException(Errors.NO_METHOD);

            Payload = request.InputStream;
            RemoteEndPoint = request.RemoteEndPoint;

            FOriginalRequest = request;

            Cancellation = cancellation;
        }

        public string? SessionId { get; }

        public string Module { get; }

        public string Method { get; }

        public Stream Payload { get; }

        public CancellationToken Cancellation { get; }

        private Dictionary<string, string>? FHeaders;
        public IReadOnlyDictionary<string, string> Headers => FHeaders ??= FOriginalRequest
            .Headers
            .AllKeys
            .ToDictionary(key => key, key => FOriginalRequest.Headers[key]);

        private Dictionary<string, string>? FRequestParameters;
        public IReadOnlyDictionary<string, string> RequestParameters => FRequestParameters ??= FOriginalRequest
            .QueryString
            .AllKeys
            .ToDictionary(key => key, key => FOriginalRequest.QueryString[key]);

        public IPEndPoint RemoteEndPoint { get; }
    }
}
