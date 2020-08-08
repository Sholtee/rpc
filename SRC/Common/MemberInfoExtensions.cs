/********************************************************************************
* MemberInfoExtensions.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    internal static class MemberInfoExtensions
    {
        public static string GetId(this MemberInfo member) => member.GetCustomAttribute<AliasAttribute>(inherit: false)?.Name ?? member.Name;
    }
}
