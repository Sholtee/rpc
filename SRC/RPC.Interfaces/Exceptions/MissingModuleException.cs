/********************************************************************************
* MissingModuleException.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc.Interfaces
{
    using Properties;

    /// <summary>
    /// The exception that is thrown when a module could not be found.
    /// </summary>
    [SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
    public class MissingModuleException: MissingMemberException
    {
        /// <summary>
        /// Creates a new <see cref="MissingModuleException"/> instance.
        /// </summary>
        public MissingModuleException(string module) : base(string.Format(Errors.Culture, Errors.MODULE_NOT_FOUND, module))
        {
        }
    }
}
