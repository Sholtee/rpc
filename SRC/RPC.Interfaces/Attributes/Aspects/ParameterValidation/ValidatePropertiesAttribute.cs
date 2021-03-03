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
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Primitives;

    /// <summary>
    /// Indicates that the properties of a parameter or property may be validated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public class ValidatePropertiesAttribute : ValidatorAttributeBase, IAsyncPropertyValidator, IAsyncParameterValidator
    {
        private MethodInfo? ContainingMethod { get; set; }

        /// <summary>
        /// Returns true if the validator should collect all the validation errors.
        /// </summary>
        public bool Aggregate { get; }

        /// <summary>
        /// Creates a new <see cref="ValidatePropertiesAttribute"/> instance.
        /// </summary>
        public ValidatePropertiesAttribute(bool aggregate = false): base(supportsNull: false) => Aggregate = aggregate;

        private async Task ValidateAsync(Type type, object value, IInjector currentScope)
        {
            Debug.Assert(ContainingMethod is not null);

            List<ValidationException> validationErrors = new();

            foreach (Func<MethodInfo, IInjector, object, Task> validate in GetValidators(type))
            {
                try
                {
                    await validate(ContainingMethod!, currentScope, value);
                }
                catch (ValidationException validationError) when (Aggregate)
                {
                    validationErrors.Add(validationError);
                }
            }

            if (validationErrors.Any())
                throw new AggregateException(validationErrors);

            static IReadOnlyCollection<Func<MethodInfo, IInjector, object, Task>> GetValidators(Type type) => ValidatorsToDelegate<Func<MethodInfo, IInjector, object, Task>>(type, (prop, validator, getter) => async (containingMethod, currentScope, instance) =>
            {
                if (validator is IConditionalValidatior conditional && !conditional.ShouldRun(containingMethod, currentScope))
                    return;

                object? value = getter(instance);

                if (value is null && !validator.SupportsNull)
                    return;

                if (validator is IAsyncPropertyValidator asyncValidator)
                {
                    await asyncValidator.ValidateAsync(prop, value, currentScope);
                    return;
                }

                validator.Validate(prop, value, currentScope);
            });
        }

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

            static IReadOnlyCollection<Action<MethodInfo, IInjector, object>> GetValidators(Type type) => ValidatorsToDelegate<Action<MethodInfo, IInjector, object>>(type, (prop, validator, getter) => (containingMethod, currentScope, instance) =>
            {
                if (validator is not IConditionalValidatior conditional || conditional.ShouldRun(containingMethod, currentScope))
                {
                    object? value = getter(instance);

                    if (value is not null || validator.SupportsNull)
                        validator.Validate(prop, value, currentScope);
                }
            });
        }
               
        private static IReadOnlyCollection<TDelegate> ValidatorsToDelegate<TDelegate>(Type type, Func<PropertyInfo, IPropertyValidator, Func<object, object?>, TDelegate> convert) where TDelegate : Delegate => Cache.GetOrAdd(type, () => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy)
            .SelectMany(prop =>
            {
                Func<object, object?> getter = prop.ToGetter();

                return prop
                    .GetCustomAttributes()
                    .OfType<IPropertyValidator>()
                    .Select(validator => convert(prop, validator, getter));
            })
            .ToArray());

        /// <inheritdoc/>
        public override bool ShouldRun(MethodInfo containingMethod, IInjector currentScope)
        {
            ContainingMethod = containingMethod;
            return base.ShouldRun(containingMethod, currentScope);
        }

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector currentScope) => Validate(param.ParameterType, value!, currentScope);

        Task IAsyncParameterValidator.ValidateAsync(ParameterInfo param, object? value, IInjector currentScope) => ValidateAsync(param.ParameterType, value!, currentScope);

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector currentScope) => Validate(prop.PropertyType, value!, currentScope);

        Task IAsyncPropertyValidator.ValidateAsync(PropertyInfo prop, object? value, IInjector currentScope) => ValidateAsync(prop.PropertyType, value!, currentScope);

        //
        // Mivel ezek explicit implementaciok ezert az attributumok alkalmazasakor nem lehet beallitani oket
        //

        string IParameterValidator.ParameterValidationErrorMessage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        string IPropertyValidator.PropertyValidationErrorMessage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
