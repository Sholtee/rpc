/********************************************************************************
* NotEmptyAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Properties;

    /// <summary>
    /// Ensures that the value of a parameter is not null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public class NotEmptyAttribute : ValidatorAttributeBase, IParameterValidator, IPropertyValidator
    {
        /// <summary>
        /// Creates a new <see cref="NotNullAttribute"/> class.
        /// </summary>
        public NotEmptyAttribute() : base(supportsNull: false) {}

        /// <summary>
        /// See <see cref="IParameterValidator.PropertyValidationMessage"/>.
        /// </summary>
        public string PropertyValidationMessage { get; set; } = Errors.EMPTY_PROPERTY;

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector _)
        {
            if (value is IEnumerable enumerable && !enumerable.Cast<object>().Any())
                throw new ValidationException(PropertyValidationMessage) 
                {
                    Name = prop.Name
                };
        }

        /// <summary>
        /// See <see cref="IPropertyValidator.ParameterValidationMessage"/>.
        /// </summary>
        public string ParameterValidationMessage { get; set; } = Errors.EMPTY_PARAM;

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector _)
        {
            if (value is IEnumerable enumerable && !enumerable.Cast<object>().Any())
                throw new ValidationException(ParameterValidationMessage)
                {
                    Name = param.Name
                };
        }
    }
}
