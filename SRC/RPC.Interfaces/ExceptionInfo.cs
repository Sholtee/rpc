/********************************************************************************
* ExceptionInfo.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes a remote exception.
    /// </summary>
    #pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    public class ExceptionInfo 
    {
        /// <summary>
        /// The assembly-qualified name of the exception type.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// The message of the remote exception.
        /// </summary>
        public string Message { get; set; }

        //
        // Nem lehet IDictionary mert akkor a tipusos deszerializalas nem mukodne.
        //

        /// <summary>
        /// Custom data associated with the exception.
        /// </summary>
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
        public IDictionary<string, string> Data { get; set; }
    }
    #pragma warning restore CS8618
}
