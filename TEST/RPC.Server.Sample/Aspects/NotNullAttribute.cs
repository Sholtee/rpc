/********************************************************************************
* NotNullAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Server.Sample
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    internal class NotNullAttribute : Attribute, IParameterValidator
    {
        public void Validate(ParameterInfo param, object? value)
        {
            if (value == null) 
                throw new ArgumentNullException(param.Name);
        }
    }
}
