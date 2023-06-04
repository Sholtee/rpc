/********************************************************************************
* MustAttribute.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Primitives;
    using Properties;

    /// <summary>
    /// Ensures that the value a parameter or property passes the given validation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class MustAttribute : ValidatorAttributeBase, IAsyncParameterValidator, IAsyncPropertyValidator
    {
        private IPredicate Predicate { get; }

        /// <summary>
        /// Creates a new <see cref="MustAttribute"/> class.
        /// </summary>
        [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments", Justification = "See 'Predicate' property")]
        public MustAttribute(Type predicate) : base(supportsNull: true)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            ConstructorInfo? ctor = predicate.GetConstructor(Type.EmptyTypes) ?? 
                throw new ArgumentException(Errors.PARAMETERLESS_CTOR_REQUIRED, nameof(predicate));

            Predicate = ctor
               .ToStaticDelegate()
               .Invoke(Array.Empty<object?>()) as IPredicate ?? throw new ArgumentException
                (
                    string.Format(Errors.Culture, Errors.NOT_ASSIGNABLE_FROM, ctor.ReflectedType, typeof(IPredicate)),
                    nameof(predicate)
                );
        }

        /// <summary>
        /// See <see cref="IPropertyValidator.PropertyValidationErrorMessage"/>.
        /// </summary>
        public string PropertyValidationErrorMessage { get; set; } = Errors.VALIDATION_FAILED;

        void IPropertyValidator.Validate(PropertyInfo prop, object? value, IInjector currentScope)
        {
            if (!Predicate.Execute(value, currentScope))
                throw new ValidationException(PropertyValidationErrorMessage)
                {
                    TargetName = prop.Name
                };
        }

        async Task IAsyncPropertyValidator.ValidateAsync(PropertyInfo prop, object? value, IInjector currentScope)
        {
            bool valid = Predicate is IAsyncPredicate asyncPredicate
                ? await asyncPredicate.ExecuteAsync(value, currentScope)
                : Predicate.Execute(value, currentScope);
            
            if (!valid)
                throw new ValidationException(PropertyValidationErrorMessage)
                {
                    TargetName = prop.Name
                };
        }

        /// <summary>
        /// See <see cref="IParameterValidator.ParameterValidationErrorMessage"/>.
        /// </summary>
        public string ParameterValidationErrorMessage { get; set; } = Errors.VALIDATION_FAILED;

        void IParameterValidator.Validate(ParameterInfo param, object? value, IInjector currentScope)
        {
            if (!Predicate.Execute(value, currentScope))
                throw new ValidationException(ParameterValidationErrorMessage)
                {
                    TargetName = param.Name
                };
        }

        async Task IAsyncParameterValidator.ValidateAsync(ParameterInfo param, object? value, IInjector currentScope)
        {
            bool valid = Predicate is IAsyncPredicate asyncPredicate
                ? await asyncPredicate.ExecuteAsync(value, currentScope)
                : Predicate.Execute(value, currentScope);

            if (!valid)
                throw new ValidationException(ParameterValidationErrorMessage)
                {
                    TargetName = param.Name
                };
        }
    }
}
