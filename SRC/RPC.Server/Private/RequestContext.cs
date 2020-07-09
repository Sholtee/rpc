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
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Properties;

    internal class RequestContext : IRequestContext
    {
        internal RequestContext(string? sessionId, string module, string method, string args, IReadOnlyDictionary<string, string> headers)
        {
            SessionId = sessionId;
            Module    = module;
            Method    = method;
            Args      = args;
            Headers   = headers;
        }

        public static async Task<RequestContext> Create(HttpListenerRequest request) 
        {
            NameValueCollection queryString = request.QueryString;

            return new RequestContext
            (
                queryString.Get("sessionid"),
                queryString.Get("module") ?? throw new InvalidOperationException(Resources.NO_MODULE),
                queryString.Get("method") ?? throw new InvalidOperationException(Resources.NO_METHOD),
                await ReadBody(),
                request.Headers.AllKeys.ToDictionary(key => key, key => request.Headers[key])

            );

            async Task<string> ReadBody()
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                return await reader.ReadToEndAsync();
            }
        }

        public string? SessionId { get; }

        public string Module { get; }

        public string Method { get; }

        public string Args { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
