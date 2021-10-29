/********************************************************************************
* ServiceMethodScopeLogger.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Creates a new log scope containing the service and method name.
    /// </summary>
    public sealed class ServiceMethodScopeLogger: LoggerBase
    {
        /// <inheritdoc/>
        public override object? Invoke(LogContext context, Func<object?> callNext)
        {
            using IDisposable? logScope = context.Logger.BeginScope(new Dictionary<string, object>
            {
                ["Service"] = context.Method.ReflectedType,
                ["Method"] = context.Method
                // TODO: szerviz nev
            });

            return callNext();
        }
    }
}
