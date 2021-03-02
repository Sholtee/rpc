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
        public override object? Invoke(MethodInfo method, object?[] args, MemberInfo extra)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (args == null)
                throw new ArgumentNullException(nameof(args));

            if (typeof(Task).IsAssignableFrom(method.ReturnType)) return AsyncExtensions.Decorate
            (
                () => (Task) base.Invoke(method, args, extra)!,
                method.ReturnType, 
                ValidateAsync
            );

            Validate();
            return base.Invoke(method, args, extra);

            async Task ValidateAsync()
            {
                List<ValidationException> exceptions = new();

                foreach (Func<IInjector, object?[], Task> validate in GetValidators(method))
                {
                    try
                    {
                        await validate(CurrentScope, args);
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

                static IReadOnlyCollection<Func<IInjector, object?[], Task>> GetValidators(MethodInfo method) => Cache.GetOrAdd(method, () => method
                    .GetParameters()
                    .SelectMany(param => param
                        .GetCustomAttributes()
                        .OfType<IParameterValidator>()
                        .Select<IParameterValidator, Func<IInjector, object?[], Task>>(validator => async (currentScope, args) =>
                        {
                            if (validator is IConditionalValidatior conditional && !conditional.ShouldRun(method, currentScope))
                                return;
    
                            object? value = args[param.Position];

                            if (value is null && !validator.SupportsNull)
                                return;

                            if (validator.SupportsAsync && validator is IAsyncParameterValidator asyncValidator)
                            {
                                await asyncValidator.ValidateAsync(param, value, currentScope);
                                return;
                            }

                            validator.Validate(param, value, currentScope);
                        }))
                    .ToArray());
            }

            void Validate()
            {
                List<ValidationException> exceptions = new();

                foreach (Action<IInjector, object?[]> validate in GetValidators(method))
                {
                    try
                    {
                        validate(CurrentScope, args);
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

                static IReadOnlyCollection<Action<IInjector, object?[]>> GetValidators(MethodInfo method) => Cache.GetOrAdd(method, () => method
                    .GetParameters()
                    .SelectMany(param => param
                        .GetCustomAttributes()
                        .OfType<IParameterValidator>()
                        .Select<IParameterValidator, Action<IInjector, object?[]>>(validator => (currentScope, args) =>
                        {
                            if (validator is not IConditionalValidatior conditional || conditional.ShouldRun(method, currentScope))
                            {
                                object? value = args[param.Position];

                                if (value is not null || validator.SupportsNull)
                                    validator.Validate(param, value, currentScope);
                            }
                        }))
                    .ToArray());
            }
        }
    }
}
