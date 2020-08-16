/********************************************************************************
* HttpException.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Represents a HTTP error.
    /// </summary>
    public class HttpException : Exception
    {
        /// <summary>
        /// Creates a new <see cref="HttpException"/> instance.
        /// </summary>
        public HttpException(string message) : this(message, null!)
        {
        }

        /// <summary>
        /// Creates a new <see cref="HttpException"/> instance.
        /// </summary>
        public HttpException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a new <see cref="HttpException"/> instance.
        /// </summary>
        public HttpException()
        {
        }

        /// <summary>
        /// The related status code.
        /// </summary>
        public HttpStatusCode Status { get; set; } = HttpStatusCode.InternalServerError;
    }
}
