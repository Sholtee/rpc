/********************************************************************************
* IRoleManager.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// Gets the roles assigned to the given <paramref name="sessionId"/>.
        /// </summary>
        /// <remarks>Returns the value of 0 if the <paramref name="sessionId"/> is null.</remarks>
        Task<Enum> GetAssignedRolesAsync(string? sessionId, CancellationToken cancellation);

        /// <summary>
        /// Optional validator to override the default behavior.
        /// </summary>
        Action<IReadOnlyList<Enum>, Enum>? ValidateFn { get; }
    }
}