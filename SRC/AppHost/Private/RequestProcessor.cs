/********************************************************************************
* RequestProcessor.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;

namespace Solti.Utils.AppHost.Internals
{
    using Properties;

    internal sealed class RequestProcessor : IRequestProcessor
    {
        public IRequestContext ProcessRequest(HttpListenerRequest request)
        {
            NameValueCollection queryString = request.QueryString;

            return new RequestContext
            (
                sessionId: queryString.Get("sessionid"),
                module:    queryString.Get("module") ?? throw new InvalidOperationException(Resources.NO_MODULE),
                method:    queryString.Get("method") ?? throw new InvalidOperationException(Resources.NO_METHOD),
                args:      ReadBody()
            );

            string ReadBody() 
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                return reader.ReadToEnd();
            }
        }
    }
}
