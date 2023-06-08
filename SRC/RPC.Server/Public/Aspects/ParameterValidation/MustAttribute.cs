/********************************************************************************
* MustAttribute.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Interfaces.Properties;

    /// <summary>
    /// Ensures that the value a parameter or property passes the given validation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class MustAttribute<TPredicate> : ValidatorAttributeBase, IParameterValidator, IPropertyValidator where TPredicate: IPredicate, new()
    {
        /// <summary>
        /// Creates a new <see cref="MustAttribute{TPredicate}"/> instance.
        /// </summary>
        /// <param name="supportsNull"></param>
        public MustAttribute(bool supportsNull = false) : base(supportsNull)
        {
        }

        private IPredicate Predicate { get; } = new TPredicate();

        /// <summary>
        /// See <see cref="IPropertyValidator.PropertyValidationErrorMessage"/>.
        /// </summary>
        public string PropertyValidationErrorMessage { get; set; } = Errors.VALIDATION_FAILED;

        void IPropertyValidator.Validate(PropertyInfo prop, object? value)
        {
            if (!Predicate.Execute(value))
                throw new ValidationException(PropertyValidationErrorMessage)
                {
                    TargetName = prop.Name
                };
        }

        /// <summary>
        /// See <see cref="IParameterValidator.ParameterValidationErrorMessage"/>.
        /// </summary>
        public string ParameterValidationErrorMessage { get; set; } = Errors.VALIDATION_FAILED;

        void IParameterValidator.Validate(ParameterInfo param, object? value)
        {
            if (!Predicate.Execute(value))
                throw new ValidationException(ParameterValidationErrorMessage)
                {
                    TargetName = param.Name
                };
        }
    }
}
