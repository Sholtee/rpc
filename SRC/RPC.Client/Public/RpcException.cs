/********************************************************************************
* RpcException.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Runtime.Serialization;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// The exception that descibes an RPC error.
    /// </summary>
    [Serializable]
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

        /// <summary>
        /// Creates a new <see cref="RpcException"/> instance.
        /// </summary>
        protected RpcException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}