/********************************************************************************
* RequiredRolesAttribute.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces.Properties;
    using Internals;

    /// <summary>
    /// Specifies the roles required for the module method invocation.
    /// </summary>
    /// <remarks>The module should be annotated with the <see cref="RoleValidatorAspectAttribute"/>.</remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequiredRolesAttribute<TRoles>: Attribute, IRequiredRolesAttribute where TRoles: Enum
    {
        /// <summary>
        /// The list of role groups required for module method invocation, e.g.: <br/>
        /// [0] MyRoles.StandardUser | MyRoles.CanPrint <br/>
        /// [1] MyRoles.Admin // admins are allowed to print by default
        /// </summary>
        /// <remarks>If no roles specified, anonymous access is allowed.</remarks>
        public IReadOnlyList<Enum> RoleGroups { get; }

        /// <summary>
        /// Creates a new <see cref="RequiredRolesAttribute{TRoles}"/> instance. You may specify more groups: <br/>
        /// [MyRoles.Admin)]
        /// </summary>
        /// <param name="roleGroup"></param>
        public RequiredRolesAttribute(TRoles roleGroup): this(new TRoles[] { roleGroup }) { }

        /// <summary>
        /// Creates a new <see cref="RequiredRolesAttribute{TRoles}"/> instance. You may specify more groups: <br/>
        /// [RequiredRoles(MyRoles.StandardUser | MyRoles.CanPrint, MyRoles.Admin)]
        /// </summary>
        public RequiredRolesAttribute(params TRoles[] roleGroups)
        {
            if (roleGroups is null)
                throw new ArgumentNullException(nameof(roleGroups));

            if (!roleGroups.Any())
                throw new ArgumentException(Errors.NO_REQUIRED_ROLE, nameof(roleGroups));

            RoleGroups = roleGroups.Cast<Enum>().ToArray();
        }
    }
}
