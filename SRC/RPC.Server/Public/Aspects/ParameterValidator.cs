/********************************************************************************
* ParameterValidator.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Primitives;
    using Proxy;

    /// <summary>
    /// Validates parameters on interface methods
    /// </summary>
    public class ParameterValidator<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
    {
        /// <summary>
        /// Returns true if the validator should collect all the validation errors.
        /// </summary>
        public bool Aggregate { get; }

        /// <summary>
        /// The current scope.
        /// </summary>
        public IInjector CurrentScope { get; }

        /// <summary>
        /// Creates a new <see cref="ParameterValidator{TInterface}"/> instance.
        /// </summary>
        public ParameterValidator(TInterface target, IInjector currentScope, bool aggregate) : base(target)
        {
            CurrentScope = currentScope;
            Aggregate = aggregate;
        }

        /// <summary>
        /// Creates a new <see cref="ParameterValidator{TInterface}"/> instance.
        /// </summary>
        [ServiceActivator]
        public ParameterValidator(TInterface target, IInjector currentScope) : this(target, currentScope, typeof(TInterface).GetCustomAttribute<ParameterValidatorAspectAttribute>()?.Aggregate ?? false) { }

        /// <inheritdoc/>
        public override object? Invoke(InvocationContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (!typeof(Task).IsAssignableFrom(context.Method.ReturnType))
            {
                Validate();
                return base.Invoke(context);
            }

            return AsyncExtensions.Before
            (
                () => (Task) base.Invoke(context)!,
                context.Method.ReturnType, 
                ValidateAsync
            );

            async Task ValidateAsync()
            {
                List<ValidationException> exceptions = new();

                foreach (Func<IInjector, object?[], Task> validate in GetValidators(context.Method))
                {
                    try
                    {
                        await validate(CurrentScope, context.Args);
                    }
                    catch (ValidationException validationError) when (Aggregate)
                    {
                        exceptions.Add(validationError);
                    }
                    catch (AggregateException validationErrors) when (Aggregate) // PropertyValidator tamogatas
                    {
                        exceptions.AddRange(validationErrors.InnerExceptions.OfType<ValidationException>());
                    }
                }

                if (exceptions.Any())
                    throw new AggregateException(exceptions);

                static IReadOnlyCollection<Func<IInjector, object?[], Task>> GetValidators(MethodInfo method) => ValidatorsToDelegate<Func<IInjector, object?[], Task>>(method, (param, validator) => async (currentScope, args) =>
                {
                    if ((validator as IConditionalValidatior)?.ShouldRun(method, currentScope) is false)
                        return;

                    object? value = args[param.Position];

                    if (value is null && !validator.SupportsNull)
                        return;

                    if (validator is IAsyncParameterValidator asyncValidator)
                    {
                        await asyncValidator.ValidateAsync(param, value, currentScope);
                        return;
                    }

                    validator.Validate(param, value, currentScope);
                });
            }

            void Validate()
            {
                List<ValidationException> exceptions = new();

                foreach (Action<IInjector, object?[]> validate in GetValidators(context.Method))
                {
                    try
                    {
                        validate(CurrentScope, context.Args);
                    }
                    catch (ValidationException validationError) when (Aggregate)
                    {
                        exceptions.Add(validationError);
                    }
                    catch (AggregateException validationErrors) when (Aggregate) // PropertyValidator tamogatas
                    {
                        exceptions.AddRange(validationErrors.InnerExceptions.OfType<ValidationException>());
                    }
                }

                if (exceptions.Any())
                    throw new AggregateException(exceptions);

                static IReadOnlyCollection<Action<IInjector, object?[]>> GetValidators(MethodInfo method) => ValidatorsToDelegate<Action<IInjector, object?[]>>(method, (param, validator) => (currentScope, args) =>
                {
                    if ((validator as IConditionalValidatior)?.ShouldRun(method, currentScope) is not false)
                    {
                        object? value = args[param.Position];

                        if (value is not null || validator.SupportsNull)
                            validator.Validate(param, value, currentScope);
                    }
                });
            }

            static IReadOnlyCollection<TDelegate> ValidatorsToDelegate<TDelegate>(MethodInfo method, Func<ParameterInfo, IParameterValidator, TDelegate> convert) where TDelegate : Delegate => Cache.GetOrAdd(method, () => method
                .GetParameters()
                .SelectMany(param => param
                    .GetCustomAttributes()
                    .OfType<IParameterValidator>()
                    .Select(validator => convert(param, validator)))
                .ToArray());
        }
    }
}
