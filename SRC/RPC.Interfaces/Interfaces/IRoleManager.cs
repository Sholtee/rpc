/********************************************************************************
* IRoleManager.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Contains some role related logic.
    /// </summary>
    public interface IRoleManager
    {
        /// <summary>
        /// Gets the roles assigned to the given <paramref name="sessionId"/>.
        /// </summary>
        /// <remarks>Returns the value of 0 if the <paramref name="sessionId"/> is null.</remarks>
        Enum GetAssignedRoles(string? sessionId);
    }
}