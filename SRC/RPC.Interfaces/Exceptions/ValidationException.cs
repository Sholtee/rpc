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
        //
        // Kivetelek nem kerulnek egy az egyben szerializalasra, ezert Data-ba mentunk (az biztosan eljut a kliensig).
        //

        /// <summary>
        /// The name of parameter or property on which the validation failed.
        /// </summary>
        public string? TargetName { get => (string?) Data[nameof(TargetName)]; set => Data[nameof(TargetName)] = value; }

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
