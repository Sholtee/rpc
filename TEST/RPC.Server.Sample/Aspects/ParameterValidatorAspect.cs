/********************************************************************************
* ParameterValidatorAspect.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Solti.Utils.DI.Interfaces;

namespace Solti.Utils.Rpc.Server.Sample
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class ParameterValidatorAspect : AspectAttribute
    {
        public override Type GetInterceptorType(Type iface)
        {
            Type interceptor = Type.GetType("Solti.Utils.Rpc.Server.Sample.ParameterValidator`1, Solti.Utils.Rpc.Server.Sample, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null", throwOnError: true)!;
            return interceptor.MakeGenericType(iface);
        }
    }
}
