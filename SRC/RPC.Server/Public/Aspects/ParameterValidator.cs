﻿/********************************************************************************
* ParameterValidator.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;
    using Interfaces;
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
        /// Creates a new <see cref="ParameterValidator{TInterface}"/> instance.
        /// </summary>
        public ParameterValidator(TInterface target, bool aggregate) : base(target) => Aggregate = aggregate;

        /// <summary>
        /// Creates a new <see cref="ParameterValidator{TInterface}"/> instance.
        /// </summary>
        [ServiceActivator]
        public ParameterValidator(TInterface target) : this(target, typeof(TInterface).GetCustomAttribute<ParameterValidatorAspectAttribute>()?.Aggregate ?? false) { }

        /// <inheritdoc/>
        public override object? Invoke(MethodInfo method, object?[] args, MemberInfo extra)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (args == null)
                throw new ArgumentNullException(nameof(args));

            List<ValidationException> exceptions = new();

            foreach (Action<object?[]> validate in GetValidators())
            {
                try
                {
                    validate(args);
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

            return base.Invoke(method, args, extra);

            IReadOnlyCollection<Action<object?[]>> GetValidators() => Cache.GetOrAdd(method, () => method
                .GetParameters()
                .SelectMany(param => param
                    .GetCustomAttributes()
                    .OfType<IParameterValidator>()
                    .Select<IParameterValidator, Action<object?[]>>(validator => args => validator.Validate(param, args[param.Position])))
                .ToArray());
        }
    }
}
