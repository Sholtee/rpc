/********************************************************************************
* NotEmptyAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Interfaces.Properties;

    /// <summary>
    /// Ensures that the value of a parameter or property is not empty.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class NotEmptyAttribute : ValidatorAttributeBase, IParameterValidator, IPropertyValidator
    {
        /// <summary>
        /// Creates a new <see cref="NotNullAttribute"/> class.
        /// </summary>
        public NotEmptyAttribute() : base(supportsNull: false) {}

        /// <summary>
        /// See <see cref="IPropertyValidator.PropertyValidationErrorMessage"/>.
        /// </summary>
        public string PropertyValidationErrorMessage { get; set; } = Errors.EMPTY_PROPERTY;

        void IPropertyValidator.Validate(PropertyInfo prop, object? value)
        {
            if (value is IEnumerable enumerable && !enumerable.Cast<object>().Any())
                throw new ValidationException(PropertyValidationErrorMessage) 
                {
                    TargetName = prop.Name
                };
        }

        /// <summary>
        /// See <see cref="IParameterValidator.ParameterValidationErrorMessage"/>.
        /// </summary>
        public string ParameterValidationErrorMessage { get; set; } = Errors.EMPTY_PARAM;

        void IParameterValidator.Validate(ParameterInfo param, object? value)
        {
            if (value is IEnumerable enumerable && !enumerable.Cast<object>().Any())
                throw new ValidationException(ParameterValidationErrorMessage)
                {
                    TargetName = param.Name
                };
        }
    }
}
