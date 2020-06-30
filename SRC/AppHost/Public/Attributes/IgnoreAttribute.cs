/********************************************************************************
* IgnoreAttribute.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.AppHost
{
    /// <summary>
    /// Ignores a member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
    public sealed class IgnoreAttribute: Attribute // TODO: move to a common interfaces project
    {
    }
}
