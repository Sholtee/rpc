/********************************************************************************
* IClock.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes an abstract clock.
    /// </summary>
    public interface IClock 
    {
        /// <summary>
        /// Returns the current time.
        /// </summary>
        DateTime UtcNow { get; }
    }
}
