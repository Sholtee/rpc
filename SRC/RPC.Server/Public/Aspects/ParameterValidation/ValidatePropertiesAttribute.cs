/********************************************************************************
* ValidatePropertiesAttribute.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Primitives;

    /// <summary>
    /// Indicates that the properties of a parameter or property may be validated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ValidatePropertiesAttribute : ValidatorAttributeBase, IPropertyValidator, IParameterValidator
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<Action<object>>> FDelegateCache = new();

        /// <summary>
        /// Returns true if the validator should collect all the validation errors.
        /// </summary>
        public bool Aggregate { get; }

        /// <summary>
        /// Creates a new <see cref="ValidatePropertiesAttribute"/> instance.
        /// </summary>
        public ValidatePropertiesAttribute(bool aggregate = false): base(supportsNull: false) => Aggregate = aggregate;

        private void Validate(Type type, object value)
        {
            List<ValidationException> validationErrors = new();

            foreach (Action<object> validate in GetValidators(type))
            {
                try
                {
                    validate(value);
                }
                catch (ValidationException validationError) when (Aggregate)
                {
                    validationErrors.Add(validationError);
                }
            }

            if (validationErrors.Any())
                throw new AggregateException(validationErrors);
        }

        private static IReadOnlyCollection<Action<object>> GetValidators(Type type)
        {
            return FDelegateCache.GetOrAdd(type, type => GetValidatorsCore(type).ToArray());

            static IEnumerable<Action<object>> GetValidatorsCore(Type type)
            {
                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy))
                {
                    InstanceMethod getter = prop.ToGetter();

                    foreach (IPropertyValidator parameterValidator in prop.GetCustomAttributes().OfType<IPropertyValidator>())
                    {
                        yield return instance =>
                        {
                            object? value = getter(instance);

                            if (value is not null || parameterValidator.SupportsNull)
                                parameterValidator.Validate(prop, value);
                        };
                    }
                }
            }
        }

        void IParameterValidator.Validate(ParameterInfo param, object? value) => Validate(param.ParameterType, value!);

        void IPropertyValidator.Validate(PropertyInfo prop, object? value) => Validate(prop.PropertyType, value!);

        //
        // Mivel ezek explicit implementaciok ezert az attributumok alkalmazasakor nem lehet beallitani oket
        //

        string IParameterValidator.ParameterValidationErrorMessage { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        string IPropertyValidator.PropertyValidationErrorMessage { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
