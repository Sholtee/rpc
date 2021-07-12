/********************************************************************************
* LoggerAspectAttribute.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Marks a module or service to be logged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class LoggerAspectAttribute: AspectAttribute
    {
        /// <summary>
        /// Creates a new <see cref="LoggerAspectAttribute"/> instance.
        /// </summary>
        [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments")]
        public LoggerAspectAttribute(params Type[] defaultLoggers) => DefaultLoggers = defaultLoggers
            .Select(Activator.CreateInstance)
            .Cast<LoggerBase>()
            .ToArray();

        /// <summary>
        /// Creates a new <see cref="LoggerAspectAttribute"/> instance.
        /// </summary>
        public LoggerAspectAttribute() => DefaultLoggers = new LoggerBase[]
        {
            new ModuleMethodScopeLogger(),         
            new ExceptionLogger(),
            new ParameterLogger(),
            new StopWatchLogger()
        };

        /// <summary>
        /// The default loggers. This value can be overridden per methods by the <see cref="LoggersAttribute"/>. 
        /// </summary>
        public IReadOnlyList<LoggerBase> DefaultLoggers { get; }

        /// <inheritdoc/>
        public override Type GetInterceptorType(Type iface)
        {
            if (iface is null)
                throw new ArgumentNullException(nameof(iface));

            //
            // Rpc.Server szerelveny verzioja megegyezik az Rpc.Interfaces szerelveny verziojaval
            //

            Type interceptor = Type.GetType($"Solti.Utils.Rpc.Aspects.Logger`1, Solti.Utils.Rpc.Server, Version = {GetType().Assembly.GetName().Version}, Culture = neutral, PublicKeyToken = null", throwOnError: true);
            return interceptor.MakeGenericType(iface);
        }
    }
}
