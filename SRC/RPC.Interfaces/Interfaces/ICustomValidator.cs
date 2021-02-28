/********************************************************************************
* ICustomValidator.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes the common functionality of all the validators.
    /// </summary>
    public interface ICustomValidator 
    {
        /// <summary>
        /// If set, it should point to a class implementing the <see cref="IConditionalValidatior"/> interface.
        /// </summary>
        Type? Condition { get; set; }

        /// <summary>
        /// Returns true if the validator can handle NULL values.
        /// </summary>
        bool SupportsNull { get; }
    }
}
