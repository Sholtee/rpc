/********************************************************************************
* IHttpResponse.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Represents an abstract HTTP respone.
    /// </summary>
    public interface IHttpResponse
    {
        /// <summary>
        /// Gets or sets the status code of the response.
        /// </summary>
        HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// The response body.
        /// </summary>
        /// <remarks>Content-Length is calculated from the <see cref="Stream.Length"/> property.</remarks>
        Stream Payload { get; }

        /// <summary>
        /// The headers being sent to the client.
        /// </summary>
        IDictionary<string, string> Headers { get; }

        /// <summary>
        /// Returns true if the response has already been closed.
        /// </summary>
        bool IsClosed { get; }

        /// <summary>
        /// Closes the curent session.
        /// </summary>
        Task Close();

        /// <summary>
        /// The original response.
        /// </summary>
        object OriginalResponse { get; }
    }
}
