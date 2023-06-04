/********************************************************************************
* ParameterValidator.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;
    using Interfaces;

    internal sealed class ParameterValidator: AspectInterceptor
    {
        private static readonly ConcurrentDictionary<MethodInfo, IReadOnlyList<Action<object?[]>>> FDelegateCache = new();

        private static void Validate(IInvocationContext context)
        {
            bool aggregate = context
                .TargetMethod
                .ReflectedType
                .GetCustomAttribute<ParameterValidatorAspectAttribute>()?
                .Aggregate ?? false;

            List<ValidationException> exceptions = new();

            foreach (Action<object?[]> validate in GetValidators(context.TargetMethod))
            {
                try
                {
                    validate(context.Args);
                }
                catch (ValidationException validationError) when (aggregate)
                {
                    exceptions.Add(validationError);
                }
                catch (AggregateException validationErrors) when (aggregate) // PropertyValidator may raise an AggregateException
                {
                    exceptions.AddRange(validationErrors.InnerExceptions.OfType<ValidationException>());
                }
            }

            if (exceptions.Any())
                throw new AggregateException(exceptions);
        }

        private static IReadOnlyCollection<Action<object?[]>> GetValidators(MethodInfo method)
        {
            return FDelegateCache.GetOrAdd(method, method => GetValidatorsCore(method).ToArray());

            static IEnumerable<Action<object?[]>> GetValidatorsCore(MethodInfo method)
            {
                foreach (ParameterInfo param in method.GetParameters())
                {
                    foreach (IParameterValidator parameterValidator in param.GetCustomAttributes().OfType<IParameterValidator>())
                    {
                        yield return args =>
                        {
                            object? value = args[param.Position];

                            if (value is not null || parameterValidator.SupportsNull)
                                parameterValidator.Validate(param, value);
                        };
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override object? Decorator(IInvocationContext context, Next<IInvocationContext, object?> callNext)
        {
            Validate(context);
            return callNext(context);
        }

        /// <inheritdoc/>
        protected override async Task<object?> DecoratorAsync(IInvocationContext context, Next<IInvocationContext, Task<object?>> callNext)
        {
            Validate(context);
            return await callNext(context);
        }
    }
}
