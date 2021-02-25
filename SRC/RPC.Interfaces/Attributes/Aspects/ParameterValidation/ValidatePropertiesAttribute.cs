/********************************************************************************
* ValidatePropertiesAttribute.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Primitives;

    /// <summary>
    /// Indicates that the properties of a parameter or property may be validated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public class ValidatePropertiesAttribute : ValidatorAttributeBase, IParameterValidator, IPropertyValidator
    {
        /// <summary>
        /// Returns true if the validator should collect all the validation errors.
        /// </summary>
        public bool Aggregate { get; }

        /// <summary>
        /// Creates a new <see cref="ValidatePropertiesAttribute"/> instance.
        /// </summary>
        public ValidatePropertiesAttribute(bool aggregate = false) => Aggregate = aggregate;

        private void Validate(Type type, object value, IInjector currentScope)
        {
            List<ValidationException> validationErrors = new();

            foreach (Action<IInjector, object> validate in GetValidators(type))
            {
                try
                {
                    validate(currentScope, value);
                }
                catch (ValidationException validationError) when (Aggregate)
                {
                    validationErrors.Add(validationError);
                }
            }

            if (validationErrors.Any())
                throw new AggregateException(validationErrors);

            static IReadOnlyCollection<Action<IInjector, object>> GetValidators(Type type) => Cache.GetOrAdd(type, () => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy)
                .SelectMany(prop =>
                {
                    Func<object, object?> getter = prop.ToGetter();

                    return prop
                        .GetCustomAttributes()
                        .OfType<IPropertyValidator>()
                        .Select<IPropertyValidator, Action<IInjector, object>>(validator => (currentScope, instance) =>
                        {
                            if (validator is not IConditionalValidatior conditional || conditional.ShouldRun(null!, currentScope))
                                validator.Validate(prop, getter(instance), currentScope);
                        });
                })
                .ToArray());
        }

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector currentScope)
        {
            if (value is not null)
                Validate(param.ParameterType, value, currentScope);
        }

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector currentScope)
        {
            if (value is not null)
                Validate(prop.PropertyType, value, currentScope);
        }
    }
}
