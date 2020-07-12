/********************************************************************************
* IRequestContext.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// Describes an abstract request context.
    /// </summary>
    public interface IRequestContext 
    {
        /// <summary>
        /// The (optional) session ID.
        /// </summary>
        public string? SessionId { get; }

        /// <summary>
        /// The module we want to invoke.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        public string Module { get;  }

        /// <summary>
        /// The module method.
        /// </summary>
        /// <remarks>The module being invoked must not use by ref parameters.</remarks>
        public string Method { get;  }

        /// <summary>
        /// The serialized arguments of the request.
        /// </summary>
        public string Args { get; }

        /// <summary>
        /// Headers sent by the client
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
