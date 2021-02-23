/********************************************************************************
* ValidationException.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Represents a validation error.
    /// </summary>
    public class ValidationException : Exception
    {
        /// <summary>
        /// The name of member on which the validation failed.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Creates a new <see cref="ValidationException"/> instance.
        /// </summary>
        public ValidationException(string message) : this(message, null!)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ValidationException"/> instance.
        /// </summary>
        public ValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ValidationException"/> instance.
        /// </summary>
        public ValidationException()
        {
        }
    }
}
