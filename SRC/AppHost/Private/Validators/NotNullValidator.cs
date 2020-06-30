/********************************************************************************
* NotNullValidator.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.AppHost.Internals
{
    using DI.Extensions.Aspects;
    

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    internal sealed class NotNullAttribute : ParameterValidatorAttribute
    {
        public override void Validate(ParameterInfo param, object value)
        {
            if (value == null) throw new ArgumentNullException(param.Name);
        }
    }
}
