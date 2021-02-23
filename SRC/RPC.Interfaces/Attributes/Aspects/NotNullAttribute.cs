/********************************************************************************
* NotNullAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    using Properties;

    /// <summary>
    /// Ensures that a parameter is not null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public class NotNullAttribute : Attribute, IParameterValidator, IPropertyValidator
    {
        void IPropertyValidator.Validate(PropertyInfo prop, object? value)
        {
            if (value is null)
                throw new ValidationException(Errors.NULL_PROPERTY) 
                {
                    Name = prop.Name
                };
        }

        void IParameterValidator.Validate(ParameterInfo param, object? value)
        {
            if (value is null)
                throw new ValidationException(Errors.NULL_PARAM)
                {
                    Name = param.Name
                };
        }
    }
}
