/********************************************************************************
* NotNullAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Aspects
{
    /// <summary>
    /// Ensures that a parameter is not null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class NotNullAttribute : Attribute, IParameterValidator
    {
        /// <summary>
        /// Implements the validation logic.
        /// </summary>
        public void Validate(ParameterInfo param, object? value)
        {
            if (value == null) 
                throw new ArgumentNullException(param.Name);
        }
    }
}
