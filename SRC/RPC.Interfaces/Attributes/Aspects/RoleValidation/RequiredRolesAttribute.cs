/********************************************************************************
* RequiredRolesAttribute.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Specifies the roles required for the module method invocation.
    /// </summary>
    /// <remarks>The module should be annotated with the <see cref="RoleValidatorAspectAttribute"/>.</remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RequiredRolesAttribute: Attribute
    {
        /// <summary>
        /// The list of role groups required for module method invocation, e.g.: <br/>
        /// [0] MyRoles.StandardUser | MyRoles.CanPrint <br/>
        /// [1] MyRoles.Admin // admins are allowed to print by default
        /// </summary>
        /// <remarks>If no roles specified, anonymous access is allowed.</remarks>
        public IReadOnlyList<Enum> RoleGroups { get; }

        /// <summary>
        /// Creates a new <see cref="RequiredRolesAttribute"/> instance. You may specify more groups: <br/>
        /// [RequiredRoles(MyRoles.StandardUser | MyRoles.CanPrint, MyRoles.Admin)]
        /// </summary>
        public RequiredRolesAttribute(params Enum[] roleGroups) => RoleGroups = roleGroups ?? throw new ArgumentNullException(nameof(roleGroups));
    }
}
