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
        /// The HTTP request.
        /// </summary>
        IHttpRequest Request { get; }

        /// <summary>
        /// The HTTP response.
        /// </summary>
        IHttpResponse Response { get; }

        /// <summary>
        /// Notifies the request processor that the server is stopped so the operation should be cancelled.
        /// </summary>
        /// <remarks>It's safe to call the <see cref="CancellationToken.ThrowIfCancellationRequested()"/> method.</remarks>
        CancellationToken Cancellation { get; }
    }
}
