/********************************************************************************
* Logger.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;
    using Interfaces;
    using Proxy;

    /// <summary>
    /// Defines an interceptor that is responsible for logging.
    /// </summary>
    public class Logger<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
    {
        /// <summary>
        /// The current scope.
        /// </summary>
        public IInjector CurrentScope { get; }

        /// <summary>
        /// The concrete logger.
        /// </summary>
        public ILogger ConcreteLogger { get; }

        /// <summary>
        /// Creates a new <see cref="Logger{TInterface}"/> instance.
        /// </summary>
        public Logger(TInterface target, IInjector currentScope, ILogger concreteLogger) : base(target ?? throw new ArgumentNullException(nameof(target)))
        {
            CurrentScope = currentScope ?? throw new ArgumentNullException(nameof(currentScope));
            ConcreteLogger = concreteLogger ?? throw new ArgumentNullException(nameof(concreteLogger));
        }

        /// <inheritdoc/>
        public override object? Invoke(InvocationContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            IReadOnlyList<LoggerBase> targets = context.Method.GetCustomAttribute<LoggersAttribute>()?.Value ?? typeof(TInterface).GetCustomAttribute<LoggerAspectAttribute>().DefaultLoggers;
            int i = 0;

            return CallNext();

            object? CallNext() => i == targets.Count
                ? base.Invoke(context)
                : targets[i++].Invoke(new LogContext { Method = context.Method, Member = context.Member, Args = context.Args, Scope = CurrentScope, Logger = ConcreteLogger }, CallNext);
        }
    }
}
