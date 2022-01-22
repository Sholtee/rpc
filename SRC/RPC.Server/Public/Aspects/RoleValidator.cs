/********************************************************************************
* RoleValidator.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Internals;
    using Properties;
    using Proxy;

    /// <summary>
    /// Contains the role validation logic.
    /// </summary>
    /// <remarks>In order to use this interceptor you have to implement and register the <see cref="IRoleManager"/> service.</remarks>
    public class RoleValidator<TInterface>: AspectInterceptor<TInterface> where TInterface: class
    {
        private IRoleManager RoleManager { get; }

        private IRpcRequestContext RequestContext { get; }

        private static IReadOnlyList<Enum> GetRequiredRoles(MethodInfo method)
        {
            RequiredRolesAttribute? attr = method.GetCustomAttribute<RequiredRolesAttribute>();

            //
            // Meg ha nem is szukseges szerep a metodus meghivasahoz, akkor is muszaj h szerepeljen az attributum
            //

            if (attr is null)
                throw new InvalidOperationException(string.Format(Errors.Culture, Errors.NO_ROLES_SPECIFIED, method.Name));

            return attr.RoleGroups;
        }

        /// <summary>
        /// Creates a new <see cref="RoleValidator{TInterface}"/> instance.
        /// </summary>
        public RoleValidator(TInterface target, IRpcRequestContext requestContext, IRoleManager roleManager) : base(target)
        {
            RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
            RoleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));   
        }

        /// <inheritdoc/>
        protected override object? Decorator(InvocationContext context, Func<object?> callNext)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            Validate(GetRequiredRoles(context.Method), RoleManager.GetAssignedRoles(RequestContext.SessionId));
            return callNext();
        }

        /// <inheritdoc/>
        protected override async Task<Task> DecoratorAsync(InvocationContext context, Func<Task> callNext)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            Validate(GetRequiredRoles(context.Method), await RoleManager.GetAssignedRolesAsync(RequestContext.SessionId, RequestContext.Cancellation));
            return callNext();
        }

        private static void Validate(IReadOnlyList<Enum> roleGroups, Enum availableRoles)
        {
            if (!roleGroups.Any(grp => availableRoles.HasFlag(grp)))
                throw new AuthenticationException(Errors.INSUFFICIENT_PRIVILEGES);
        }
    }
}
