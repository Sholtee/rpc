﻿/********************************************************************************
* MatchAttribute.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Properties;

    /// <summary>
    /// Ensures that the string representation of a parameter or property matches the given pattern.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class MatchAttribute : ValidatorAttributeBase, IParameterValidator, IPropertyValidator
    {
        private Regex Regex { get; }

        /// <summary>
        /// Creates a new <see cref="MatchAttribute"/> instance.
        /// </summary>
        [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments", Justification = "It would make no sense to define these properties.")]
        public MatchAttribute(string pattern, RegexOptions options = RegexOptions.Compiled): base(supportsNull: false) => Regex = new Regex(pattern, options);

        /// <summary>
        /// The message that is thrown when the match was not successful.
        /// </summary>
        public string PropertyValidationErrorMessage { get; set; } = Errors.PROPERTY_NOT_MATCHES;

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector _)
        {
            if (!Regex.Match(value!.ToString()).Success)
                throw new ValidationException(PropertyValidationErrorMessage) 
                {
                    TargetName = prop.Name
                };
        }

        /// <summary>
        /// The message that is thrown when the match was not successful.
        /// </summary>
        public string ParameterValidationErrorMessage { get; set; } = Errors.PARAM_NOT_MATCHES;

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector _)
        {
            if (!Regex.Match(value!.ToString()).Success)
                throw new ValidationException(ParameterValidationErrorMessage)
                {
                    TargetName = param.Name
                };
        }
    }
}
