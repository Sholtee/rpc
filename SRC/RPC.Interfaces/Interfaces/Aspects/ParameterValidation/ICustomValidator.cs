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
        /// Returns true if the validator can handle NULL values.
        /// </summary>
        bool SupportsNull { get; }

        /// <summary>
        /// Returns true if the validator supports asynchronous invocation.
        /// </summary>
        bool SupportsAsync { get; }
    }
}
