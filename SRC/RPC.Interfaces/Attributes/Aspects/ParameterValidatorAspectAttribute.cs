/********************************************************************************
* ParameterValidatorAspectAttribute.cs                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Indicates that the methods of a service may validate their parameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class ParameterValidatorAspectAttribute : AspectAttribute
    {
        /// <summary>
        /// Returns true if the validator should collect all the validation errors.
        /// </summary>
        public bool Aggregate { get; }

        /// <summary>
        /// Creates a new <see cref="ParameterValidatorAspectAttribute"/> instance.
        /// </summary>
        public ParameterValidatorAspectAttribute(bool aggregate = false) => Aggregate = aggregate;

        /// <inheritdoc/>
        /// <inheritdoc/>
        public override Type GetInterceptorType(Type iface)
        {
            if (iface is null)
                throw new ArgumentNullException(nameof(iface));

            //
            // Rpc.Server szerelveny verzioja megegyezik az Rpc.Interfaces szerelveny verziojaval
            //

            Type interceptor = Type.GetType($"Solti.Utils.Rpc.Aspects.ParameterValidator`1, Solti.Utils.Rpc.Server, Version = {GetType().Assembly.GetName().Version}, Culture = neutral, PublicKeyToken = null", throwOnError: true);
            return interceptor.MakeGenericType(iface);
        }
    }
}
