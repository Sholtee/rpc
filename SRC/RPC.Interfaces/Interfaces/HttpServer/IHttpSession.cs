/********************************************************************************
* IHttpSession.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Represents an abstract HTTP session.
    /// </summary>
    public interface IHttpSession
    {
        /// <summary>
        /// Gets the server instance that started this session.
        /// </summary>
        IHttpServer Server { get; }

        /// <summary>
        /// The HTTP request.
        /// </summary>
        IHttpRequest Request { get; }

        /// <summary>
        /// The HTTP response.
        /// </summary>
        IHttpResponse Response { get; }
    }
}
