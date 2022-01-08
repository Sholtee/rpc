/********************************************************************************
* IRequestContext.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Encapsulates all the informations related to a request.
    /// </summary>
    public interface IRequestContext 
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
        /// All the request parameters, including custom ones.
        /// </summary>
        IReadOnlyDictionary<string, string> RequestParameters { get; }

        /// <summary>
        /// The payload of the request. It may contain serialized method parameters or raw data related to the method invocation.
        /// </summary>
        Stream Payload { get; }

        /// <summary>
        /// Notifies the request processor that the operation should be canceled.
        /// </summary>
        CancellationToken Cancellation { get; }

        /// <summary>
        /// Headers sent by the client
        /// </summary>
        IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets the original request.
        /// </summary>
        HttpListenerRequest OriginalRequest { get; }
    }
}
