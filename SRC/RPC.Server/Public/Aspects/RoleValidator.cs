/********************************************************************************
* RoleValidator.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Reflection;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Properties;
    using Primitives;
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

            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                return Invoke();

            if (!method.ReturnType.IsGenericType)
                return InvokeAsync();

            Func<Task<object>> invokeAsync = InvokeAsyncHavingResult<object>;

            return invokeAsync
                .Method
                .GetGenericMethodDefinition()
                .MakeGenericMethod(method.ReturnType.GetGenericArguments().Single())
                .ToInstanceDelegate()
                .Invoke(invokeAsync.Target, Array.Empty<object?>());
   
            async Task<T> InvokeAsyncHavingResult<T>() 
            {
                Validate(await RoleManager.GetAssignedRolesAsync(RequestContext.SessionId, RequestContext.Cancellation));
                return await (Task<T>) base.Invoke(method, args, extra)!;
            }

            async Task InvokeAsync() 
            {
                Validate(await RoleManager.GetAssignedRolesAsync(RequestContext.SessionId, RequestContext.Cancellation));
                await (Task) base.Invoke(method, args, extra)!;
            }

            object? Invoke() 
            {
                Validate(RoleManager.GetAssignedRoles(RequestContext.SessionId));
                return base.Invoke(method, args, extra);
            }

            void Validate(Enum availableRoles)
            {
                if (!attr.RoleGroups.Any(grp => availableRoles.HasFlag(grp)))
                    throw new AuthenticationException(Errors.INSUFFICIENT_PRIVILEGES);
            }
        }
    }
}
