/********************************************************************************
* RpcException.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// The exception that descibes an RPC error.
    /// </summary>
    /// <remarks>The inner exception contains the actual remote error.</remarks>
    public class RpcException : Exception
    {
        /// <summary>
        /// Creates a new <see cref="RpcException"/> instance.
        /// </summary>
        public RpcException()
        {
        }

        /// <summary>
        /// Creates a new <see cref="RpcException"/> instance.
        /// </summary>
        public RpcException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates a new <see cref="RpcException"/> instance.
        /// </summary>
        public RpcException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}