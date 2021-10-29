/********************************************************************************
* LogContext.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
#pragma warning disable CS3003 // Type is not CLS-compliant
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

using System.Collections.Generic;
using System.Reflection;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Contains the log context.
    /// </summary>
    public sealed class LogContext
    {
        /// <summary>
        /// The interface method which was invoked.
        /// </summary>
        public MethodInfo Method { get; init; }

        /// <summary>
        /// The arguments passed to the invocation.
        /// </summary>
        public IReadOnlyList<object?> Args { get; init; }

        /// <summary>
        /// The member from which the <see cref="Method"/> was extracted. For e.g. a <see cref="PropertyInfo"/>.
        /// </summary>
        public MemberInfo Member { get; init; }

        /// <summary>
        /// The current scope.
        /// </summary>
        public IInjector Scope { get; init; }

        /// <summary>
        /// The concrete logger, related to the current scope.
        /// </summary>
        public ILogger Logger { get; init; }
    }
}
