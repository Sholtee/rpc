/********************************************************************************
* NotNullAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Properties;

    /// <summary>
    /// Ensures that a parameter is not null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public class NotNullAttribute : ValidatorAttributeBase, IParameterValidator, IPropertyValidator
    {
        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector _)
        {
            if (value is null)
                throw new ValidationException(Errors.NULL_PROPERTY) 
                {
                    Name = prop.Name
                };
        }

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector _)
        {
            if (value is null)
                throw new ValidationException(Errors.NULL_PARAM)
                {
                    Name = param.Name
                };
        }
    }
}
