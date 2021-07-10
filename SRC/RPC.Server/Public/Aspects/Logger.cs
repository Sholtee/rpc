/********************************************************************************
* Logger.cs                                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
#pragma warning disable CS3003 // Type is not CLS-compliant
#pragma warning disable CS3001 // Argument type is not CLS-compliant

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
    /// Contains logging realted logic.
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
        public override object? Invoke(MethodInfo method, object?[] args, MemberInfo extra)
        {
            IReadOnlyList<LoggerBase> targets = method.GetCustomAttribute<LoggersAttribute>()?.Value ?? typeof(TInterface).GetCustomAttribute<LoggerAspectAttribute>().DefaultLoggers;
            int i = 0;

            return CallNext();

            object? CallNext() => i == targets.Count
                ? base.Invoke(method, args, extra)
                : targets[i++].Invoke(new LogContext { Method = method, Member = extra, Args = args, Scope = CurrentScope, Logger = ConcreteLogger }, CallNext);
        }
    }
}
