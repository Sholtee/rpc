/********************************************************************************
* TransactionAspectAttribute.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;

    /// <summary>
    /// Indicates that the methods of a service may use transactions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class TransactionAspectAttribute : AspectAttribute
    {
        /// <inheritdoc/>
        public override Type GetInterceptorType(Type iface)
        {
            if (iface is null)
                throw new ArgumentNullException(nameof(iface));

            //
            // Rpc.Server szerelveny verzioja megegyezik az Rpc.Interfaces szerelveny verziojaval
            //

            Type interceptor = Type.GetType($"Solti.Utils.Rpc.Aspects.TransactionManager`1, Solti.Utils.Rpc.Server, Version = {GetType().Assembly.GetName().Version}, Culture = neutral, PublicKeyToken = null", throwOnError: true);
            return interceptor.MakeGenericType(iface);
        }
    }
}
