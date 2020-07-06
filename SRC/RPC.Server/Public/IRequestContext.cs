/********************************************************************************
* IRequestContext.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// Describes a request context.
    /// </summary>
    public interface IRequestContext 
    {
        /// <summary>
        /// The (optional) session ID related to the caller.
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
        /// <remarks>It must not use by ref parameters.</remarks>
        public string Method { get;  }

        /// <summary>
        /// The serialized arguments of the request.
        /// </summary>
        public string Args { get; }
    }
}
