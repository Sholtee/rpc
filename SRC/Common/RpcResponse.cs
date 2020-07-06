﻿/********************************************************************************
* RpcResponse.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// Describes a remote exception.
    /// </summary>
    #pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    public class ExceptionInfo 
    {
        /// <summary>
        /// The full name of the exception type.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// The textual description of the error.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Custom user data related to this error.
        /// </summary>
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
        public IDictionary Data { get; set; }
    }
    #pragma warning restore CS8618

    /// <summary>
    /// Describes the response to be serialized and sent to the client.
    /// </summary>
    public class RpcResponse
    {
        /// <summary>
        /// The result (if the remote method call completed successfully).
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// The exception (if something went wrong).
        /// </summary>
        public ExceptionInfo? Exception { get; set; }
    }
}
