/********************************************************************************
* RoleValidatorAspectAttribute.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Indicates that the caller may have sufficient privileges to call the module methods.
    /// </summary>
    /// <remarks>In order to use this aspect you have to implement and register the <see cref="IRoleManager"/> service.</remarks>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class RoleValidatorAspectAttribute : AspectAttribute
    {
        /// <inheritdoc/>
        public override Type GetInterceptorType(Type iface)
        {
            if (iface is null)
                throw new ArgumentNullException(nameof(iface));

            //
            // Rpc.Server szerelveny verzioja megegyezik az Rpc.Interfaces szerelveny verziojaval
            //

            Type interceptor = Type.GetType($"Solti.Utils.Rpc.Aspects.RoleValidator`1, Solti.Utils.Rpc.Server, Version = {GetType().Assembly.GetName().Version}, Culture = neutral, PublicKeyToken = null", throwOnError: true);
            return interceptor.MakeGenericType(iface);
        }
    }
}
