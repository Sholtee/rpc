/********************************************************************************
* ValidatePropertiesAttribute.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private MethodInfo? ContainingMethod { get; set; }

        /// <summary>
        /// Returns true if the validator should collect all the validation errors.
        /// </summary>
        public bool Aggregate { get; }

        //
        // Mivel ezek explicit implementaciok ezert az attributumok alkalmazasakor nem lehet beallitani oket
        //

        string IParameterValidator.ParameterValidationErrorMessage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        string IPropertyValidator.PropertyValidationErrorMessage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Creates a new <see cref="ValidatePropertiesAttribute"/> instance.
        /// </summary>
        public ValidatePropertiesAttribute(bool aggregate = false): base(supportsNull: false) => Aggregate = aggregate;

        private void Validate(Type type, object value, IInjector currentScope)
        {
            Debug.Assert(ContainingMethod is not null);

            List<ValidationException> validationErrors = new();

            foreach (Action<MethodInfo, IInjector, object> validate in GetValidators(type))
            {
                try
                {
                    validate(ContainingMethod!, currentScope, value);
                }
                catch (ValidationException validationError) when (Aggregate)
                {
                    validationErrors.Add(validationError);
                }
            }

            if (validationErrors.Any())
                throw new AggregateException(validationErrors);

            static IReadOnlyCollection<Action<MethodInfo, IInjector, object>> GetValidators(Type type) => Cache.GetOrAdd(type, () => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy)
                .SelectMany(prop =>
                {
                    Func<object, object?> getter = prop.ToGetter();

                    return prop
                        .GetCustomAttributes()
                        .OfType<IPropertyValidator>()
                        .Select<IPropertyValidator, Action<MethodInfo, IInjector, object>>(validator => (containingMethod, currentScope, instance) =>
                        {
                            if (validator is not IConditionalValidatior conditional || conditional.ShouldRun(containingMethod, currentScope))
                            {
                                object? value = getter(instance);

                                if (value is not null || validator.SupportsNull)
                                    validator.Validate(prop, value, currentScope);
                            }
                        });
                })
                .ToArray());
        }

        /// <inheritdoc/>
        public override bool ShouldRun(MethodInfo containingMethod, IInjector currentScope)
        {
            ContainingMethod = containingMethod;
            return base.ShouldRun(containingMethod, currentScope);
        }

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector currentScope) => Validate(param.ParameterType, value!, currentScope);

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector currentScope) => Validate(prop.PropertyType, value!, currentScope);
    }
}
