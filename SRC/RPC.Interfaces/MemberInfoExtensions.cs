/********************************************************************************
* MemberInfoExtensions.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Contains some <see cref="MemberInfo"/> extensions.
    /// </summary>
    public static class MemberInfoExtensions
    {
        /// <summary>
        /// Gets the ID of a member.
        /// </summary>
        public static string GetId(this MemberInfo member)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            return member.GetCustomAttribute<AliasAttribute>(inherit: false)?.Name ?? member.Name;
        }
    }
}
