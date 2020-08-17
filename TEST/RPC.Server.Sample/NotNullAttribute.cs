/********************************************************************************
* NotNullAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Server.Sample
{
    using DI.Extensions.Aspects;

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    internal class NotNullAttribute : ParameterValidatorAttribute
    {
        public override void Validate(ParameterInfo param, object value)
        {
            if (value == null) 
                throw new ArgumentNullException(param.Name);
        }
    }
}
