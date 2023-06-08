/********************************************************************************
* IHttpRequest.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Represents an abstract HTTP request.
    /// </summary>
    public interface IHttpRequest
    {
        /// <summary>
        /// The headers sent by the client.
        /// </summary>
        IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>
        /// The query parameters of the request.
        /// </summary>
        IReadOnlyDictionary<string, string> QueryParameters { get; }

        /// <summary>
        /// The request id.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The URL of the requested ersource.
        /// </summary>
        Uri Url { get; }

        /// <summary>
        /// Content type
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// The HTTP method.
        /// </summary>
        string Method { get; }

        /// <summary>
        /// The body of the request.
        /// </summary>
        Stream Payload { get; }

        /// <summary>
        /// The IP of the client.
        /// </summary>
        IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// The original request.
        /// </summary>
        object OriginalRequest { get; }
    }
}
