/********************************************************************************
* LengthBetweenAttribute.cs                                                     *
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
    /// Ensures that the string representation of a parameter or property matches the given pattern.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public class LengthBetweenAttribute : ValidatorAttributeBase, IParameterValidator, IPropertyValidator
    {
        /// <summary>
        /// Creates a new <see cref="LengthBetweenAttribute"/> instance.
        /// </summary>
        public LengthBetweenAttribute(uint min = 0, uint max = uint.MaxValue) : base(supportsNull: false) 
        {
            Min = min;
            Max = max;

            ParameterValidationErrorMessage = string.Format(Errors.Culture, Errors.INVALID_PARAM_LENGTH, Min, Max);
            PropertyValidationErrorMessage = string.Format(Errors.Culture, Errors.INVALID_PROPERTY_LENGTH, Min, Max);
        }

        /// <summary>
        /// The minimum length of the value.
        /// </summary>
        public uint Min { get; }

        /// <summary>
        /// The maximum length of the value.
        /// </summary>
        public uint Max { get; }

        /// <summary>
        /// See <see cref="IParameterValidator.ParameterValidationErrorMessage"/>.
        /// </summary>
        public string ParameterValidationErrorMessage { get; set; }

        /// <summary>
        /// See <see cref="IPropertyValidator.PropertyValidationErrorMessage"/>.
        /// </summary>
        public string PropertyValidationErrorMessage { get; set; }

        private static int GetCount(object value) => (value as ICollection)?.Count ?? ((IEnumerable) value).Cast<object>().Count();

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector currentScope)
        {
            int count = GetCount(value!);

            if (count < Min || count > Max)
                throw new ValidationException(ParameterValidationErrorMessage)
                {
                    Name = param.Name
                };
        }

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector currentScope)
        {
            int count = GetCount(value!);

            if (count < Min || count > Max)
                throw new ValidationException(PropertyValidationErrorMessage)
                {
                    Name = prop.Name
                };
        }
    }
}
