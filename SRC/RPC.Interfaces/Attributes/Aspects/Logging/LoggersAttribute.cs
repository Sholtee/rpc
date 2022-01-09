/********************************************************************************
* LoggersAttribute.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Solti.Utils.Rpc.Interfaces
{
    using Primitives;
    using Properties;

    /// <summary>
    /// Collects the related loogers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class LoggersAttribute : Attribute
    {
        /// <summary>
        /// The logger instances.
        /// </summary>
        public IReadOnlyList<LoggerBase> Value { get; }

        /// <summary>
        /// Creates a new <see cref="LoggersAttribute"/> instance.
        /// </summary>>
        [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments")]
        public LoggersAttribute(params Type[] types) => Value = types
            .Select
            (
                type => type.GetConstructor(Array.Empty<Type>()) ?? throw new ArgumentException
                (
                    string.Format(Errors.Culture, Errors.PARAMETERLESS_CTOR_REQUIRED, type),
                    nameof(types)
                )
            )
            .Select
            (
                ctor => ctor.ToStaticDelegate().Invoke(Array.Empty<object?>()) as LoggerBase ?? throw new ArgumentException
                (
                    string.Format(Errors.Culture, Errors.NOT_ASSIGNABLE_FROM, ctor.ReflectedType, typeof(LoggerBase)),
                    nameof(types)
                )
            )
            .ToArray();

        /// <summary>
        /// Creates a new <see cref="LoggersAttribute"/> instance.
        /// </summary>>
        public LoggersAttribute() => Value = Array.Empty<LoggerBase>();
    }
}
