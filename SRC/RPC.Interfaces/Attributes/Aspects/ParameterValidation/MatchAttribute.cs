/********************************************************************************
* MatchAttribute.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Properties;

    /// <summary>
    /// Ensures that the string representation of a parameter matches the given pattern.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public class MatchAttribute : ValidatorAttributeBase, IParameterValidator, IPropertyValidator
    {
        private Regex Regex { get; }

        /// <summary>
        /// Creates a new <see cref="MatchAttribute"/> instance.
        /// </summary>
        public MatchAttribute(string pattern, RegexOptions options = RegexOptions.Compiled): base(supportsNull: false) => Regex = new Regex(pattern, options);

        /// <summary>
        /// The message that is thrown when the match was not successful.
        /// </summary>
        public string PropertyValidationMessage { get; set; } = Errors.PROPERTY_NOT_MATCHES;

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector _)
        {
            if (!Regex.Match(value!.ToString()).Success)
                throw new ValidationException(PropertyValidationMessage) 
                {
                    Name = prop.Name
                };
        }

        /// <summary>
        /// The message that is thrown when the match was not successful.
        /// </summary>
        public string ParameterValidationMessage { get; set; } = Errors.PARAM_NOT_MATCHES;

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector _)
        {
            if (!Regex.Match(value!.ToString()).Success)
                throw new ValidationException(ParameterValidationMessage)
                {
                    Name = param.Name
                };
        }
    }
}
