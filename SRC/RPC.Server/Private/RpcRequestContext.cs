/********************************************************************************
* RpcRequestContext.cs                                                          *
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

    internal sealed class RpcRequestContext : IRpcRequestContext
    {
        private readonly HttpListenerRequest FOriginalRequest;
#if DEBUG || PERF
        internal RpcRequestContext(string? sessionId, string module, string method, IPEndPoint remoteEndPoint, Stream payload, CancellationToken cancellation)
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
        public RpcRequestContext(HttpListenerRequest request, CancellationToken cancellation)
        {
            //
            // Metodus validalasa (POST eseten a keresnek kene legyen torzse).
            //

            if (request.HttpMethod.ToUpperInvariant() is not "POST")
                throw new HttpException(Errors.HTTP_METHOD_NOT_SUPPORTED) { Status = HttpStatusCode.MethodNotAllowed };

            //
            // Tartalom validalasa. ContentType property bar nem nullable de lehet NULL.
            //

            if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) is not true)
                throw new HttpException(Errors.HTTP_CONTENT_NOT_SUPPORTED) { Status = HttpStatusCode.BadRequest };

            if (request.ContentEncoding?.WebName.Equals("utf-8", StringComparison.OrdinalIgnoreCase) is not true)
                throw new HttpException(Errors.HTTP_ENCODING_NOT_SUPPORTED) { Status = HttpStatusCode.BadRequest };

            NameValueCollection paramz = request.QueryString;

            //
            // Szukseges parameterek lekerdezese (nem kis-nagy betu erzekeny).
            //

            SessionId = paramz[nameof(SessionId)];
            Module    = paramz[nameof(Module)] ?? throw new HttpException(Errors.NO_MODULE) { Status = HttpStatusCode.BadRequest };
            Method    = paramz[nameof(Method)] ?? throw new HttpException(Errors.NO_METHOD) { Status = HttpStatusCode.BadRequest };

            Payload          = request.InputStream;
            RemoteEndPoint   = request.RemoteEndPoint;
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
