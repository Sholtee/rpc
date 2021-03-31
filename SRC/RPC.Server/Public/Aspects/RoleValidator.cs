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
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Properties;
    using Proxy;

    /// <summary>
    /// Contains the role validation logic.
    /// </summary>
    /// <remarks>In order to use this interceptor you have to implement and register the <see cref="IRoleManager"/> service. Optionally you may implement the <see cref="IAsyncRoleManager"/> interface as well. In this case async methods will be validated by async validation logic.</remarks>
    public class RoleValidator<TInterface>: InterfaceInterceptor<TInterface> where TInterface: class
    {
        private IRoleManager RoleManager { get; }

        private IAsyncRoleManager? AsyncRoleManager { get; }

        private IRequestContext RequestContext { get; }

        /// <summary>
        /// Creates a new <see cref="RoleValidator{TInterface}"/> instance.
        /// </summary>
        public RoleValidator(TInterface target, IRequestContext requestContext, IRoleManager roleManager, [Options(Optional = true)] IAsyncRoleManager? asyncRoleManager) : base(target)
        {
            RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
            RoleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));   
            AsyncRoleManager = asyncRoleManager;
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

            //
            // Aszinkron szerep validalas csak akkor jatszik h hozza kapcsolodo logika implementalva lett
            // (kulonben aszinkron metodusnal is szinkron szerep validalas van)
            //

            if (typeof(Task).IsAssignableFrom(method.ReturnType) && AsyncRoleManager is not null)
                return AsyncExtensions.Before
                (
                    () => (Task) base.Invoke(method, args, extra)!, 
                    method.ReturnType, 
                    async () => Validate(await AsyncRoleManager.GetAssignedRolesAsync(RequestContext.SessionId, RequestContext.Cancellation))
                );

            Validate(RoleManager.GetAssignedRoles(RequestContext.SessionId));
            return base.Invoke(method, args, extra);

            void Validate(Enum availableRoles)
            {
                if (!attr.RoleGroups.Any(grp => availableRoles.HasFlag(grp)))
                    throw new AuthenticationException(Errors.INSUFFICIENT_PRIVILEGES);
            }
        }
    }
}
