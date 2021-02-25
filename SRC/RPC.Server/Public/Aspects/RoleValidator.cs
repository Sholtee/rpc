/********************************************************************************
* RoleValidator.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Reflection;
using System.Security.Authentication;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Properties;
    using Proxy;

    /// <summary>
    /// Contains the role validation logic.
    /// </summary>
    /// <remarks>In order to use this interceptor you have to implement and register the <see cref="IRoleManager"/> service.</remarks>
    public class RoleValidator<TInterface>: InterfaceInterceptor<TInterface> where TInterface: class
    {
        private IRoleManager RoleManager { get; }

        private IRequestContext RequestContext { get; }

        /// <summary>
        /// Creates a new <see cref="RoleValidator{TInterface}"/> instance.
        /// </summary>
        public RoleValidator(TInterface target, IRequestContext requestContext, IRoleManager roleManager) : base(target)
        {
            RoleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        }

        /// <inheritdoc/>
        public override object? Invoke(MethodInfo method, object?[] args, MemberInfo extra)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            RequiredRolesAttribute? attr = method.GetCustomAttribute<RequiredRolesAttribute>();

            //
            // Meg ha nem is szukseges szerep a metodus meghivasahoz, akkor is muszaj h szerepeljen az attributum
            //

            if (attr is null)
                throw new InvalidOperationException(string.Format(Errors.Culture, Errors.NO_ROLES_SPECIFIED, method.Name));

            Enum availableRoles = RoleManager.GetAssignedRoles(RequestContext.SessionId);

            if (!attr.RoleGroups.Any(grp => availableRoles.HasFlag(grp)))
                throw new AuthenticationException(Errors.INSUFFICIENT_PRIVILEGES);

            return base.Invoke(method, args, extra);
        }
    }
}
