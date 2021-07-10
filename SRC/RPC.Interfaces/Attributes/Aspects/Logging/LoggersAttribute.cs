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
    /// <summary>
    /// Collects the related loogers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    #pragma warning disable CS3015 // Type has no accessible constructors which use only CLS-compliant types
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
            .Select(Activator.CreateInstance)
            .Cast<LoggerBase>()
            .ToArray();
    }
}
