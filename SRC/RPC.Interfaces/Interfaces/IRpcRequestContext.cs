/********************************************************************************
* IRpcRequestContext.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Encapsulates all the informations related to a request.
    /// </summary>
    public interface IRpcRequestContext 
    {
        /// <summary>
        /// The (optional) session ID.
        /// </summary>
        string? SessionId { get; }

        /// <summary>
        /// The module we want to invoke.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        string Module { get; }

        /// <summary>
        /// The module method.
        /// </summary>
        /// <remarks>The module being invoked must not use by ref parameters.</remarks>
        string Method { get; }

        /// <summary>
        /// Notifies the request processor that the operation should be canceled.
        /// </summary>
        CancellationToken Cancellation { get; }

        /// <summary>
        /// Gets the payload containing the request parameters.
        /// </summary>
        Stream Payload { get; }

        /// <summary>
        /// Gets the original request.
        /// </summary>
        IHttpRequest OriginalRequest { get; }
    }
}
