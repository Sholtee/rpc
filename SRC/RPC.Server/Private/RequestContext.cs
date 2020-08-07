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
    using Properties;

    internal class RequestContext : IRequestContext
    {
        internal RequestContext(string? sessionId, string module, string method, Stream payload, CancellationToken cancellation)
        {
            SessionId    = sessionId;
            Module       = module;
            Method       = method;
            Payload      = payload;
            Cancellation = cancellation;

            Headers           = new Dictionary<string, string>();
            RequestParameters = new Dictionary<string, string>();
        }

        public RequestContext(HttpListenerRequest request, CancellationToken cancellation)
        {
            NameValueCollection paramz = request.QueryString;
            RequestParameters = paramz.AllKeys.ToDictionary(key => key, key => paramz[key]);

            //
            // Ne a RequestParameters-bol kerjuk le mert az kivetelt dob ha nincs elem adott kulccsal
            //

            SessionId = paramz["sessionid"];
            Module    = paramz["module"] ?? throw new InvalidOperationException(Resources.NO_MODULE);
            Method    = paramz["method"] ?? throw new InvalidOperationException(Resources.NO_METHOD);

            Payload = request.InputStream;
            Headers = request.Headers.AllKeys.ToDictionary(key => key, key => request.Headers[key]);

            Cancellation = cancellation;
        }

        public string? SessionId { get; }

        public string Module { get; }

        public string Method { get; }

        public Stream Payload { get; }

        public CancellationToken Cancellation { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public IReadOnlyDictionary<string, string> RequestParameters { get; }
    }
}
