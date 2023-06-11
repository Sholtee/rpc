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

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;
    using Interfaces;
    using Properties;

    internal interface IRequiredRolesAttribute
    {
        IReadOnlyList<Enum> RoleGroups { get; }
    }

    /// <summary>
    /// Contains the role validation logic.
    /// </summary>
    /// <remarks>In order to use this interceptor you have to implement and register the <see cref="IRoleManager"/> service.</remarks>
    internal sealed class RoleValidator: AspectInterceptor
    {
        private IRoleManager RoleManager { get; }

        private IRpcRequestContext RequestContext { get; }

        private static IReadOnlyList<Enum> GetRequiredRoles(MethodInfo method)
        {
            IRequiredRolesAttribute? attr = method
                .GetCustomAttributes()
                .OfType<IRequiredRolesAttribute>()
                .SingleOrDefault();

            //
            // Meg ha nem is szukseges szerep a metodus meghivasahoz, akkor is muszaj h szerepeljen az attributum
            //

            if (attr is null)
                throw new InvalidOperationException(string.Format(Errors.Culture, Errors.NO_ROLES_SPECIFIED, method.Name));

            return attr.RoleGroups;
        }

        public RoleValidator(IRpcRequestContext requestContext, IRoleManager roleManager)
        {
            RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
            RoleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));   
        }

        /// <inheritdoc/>
        protected override object? Decorator(IInvocationContext context, Next<IInvocationContext, object?> callNext)
        {
            Validate
            (
                GetRequiredRoles(context.TargetMethod),
                RoleManager.GetAssignedRoles(RequestContext.SessionId)
            );

            return callNext(context);
        }

        /// <inheritdoc/>
        protected override async Task<object?> DecoratorAsync(IInvocationContext context, Next<IInvocationContext, Task<object?>> callNext)
        {
            Validate
            (
                GetRequiredRoles(context.TargetMethod),
                await RoleManager.GetAssignedRolesAsync(RequestContext.SessionId, RequestContext.Cancellation)
            );

            return await callNext(context);
        }

        private void Validate(IReadOnlyList<Enum> roleGroups, Enum availableRoles)
        {
            if (RoleManager.ValidateFn is not null)
            {
                RoleManager.ValidateFn(roleGroups, availableRoles);
                return;
            }

            foreach (Enum roleGroup in roleGroups)
            {
                if (availableRoles.HasFlag(roleGroup))
                    return;
            }

            throw new AuthenticationException(Errors.INSUFFICIENT_PRIVILEGES);
        }
    }
}
