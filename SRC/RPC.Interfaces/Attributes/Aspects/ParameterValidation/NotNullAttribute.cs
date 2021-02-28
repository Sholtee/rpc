﻿/********************************************************************************
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
        /// <summary>
        /// Creates a new <see cref="NotNullAttribute"/> class.
        /// </summary>
        public NotNullAttribute() : base(supportsNull: true) {}

        /// <summary>
        /// See <see cref="IParameterValidator.PropertyValidationErrorMessage"/>.
        /// </summary>
        public string PropertyValidationErrorMessage { get; set; } = Errors.NULL_PROPERTY;
        
        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector _)
        {
            if (value is null)
                throw new ValidationException(PropertyValidationErrorMessage) 
                {
                    Name = prop.Name
                };
        }

        /// <summary>
        /// See <see cref="IPropertyValidator.ParameterValidationErrorMessage"/>.
        /// </summary>
        public string ParameterValidationErrorMessage { get; set; } = Errors.NULL_PARAM;

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector _)
        {
            if (value is null)
                throw new ValidationException(ParameterValidationErrorMessage)
                {
                    Name = param.Name
                };
        }
    }
}
