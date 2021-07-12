/********************************************************************************
* ModuleMethodScopeLogger.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Creates a new log scope containing the module and method name and the session id.
    /// </summary>
    /// <remarks>This logger is intended to be used on modules only.</remarks>
    public sealed class ModuleMethodScopeLogger: LoggerBase
    {
        /// <inheritdoc/>
        public override object? Invoke(LogContext context, Func<object?> callNext)
        {
            IRequestContext cntx = context.Scope.Get<IRequestContext>();

            using IDisposable? logScope = context.Logger.BeginScope(new Dictionary<string, object>
            {
                [nameof(cntx.Module)]    = cntx.Module,
                [nameof(cntx.Method)]    = cntx.Method,
                [nameof(cntx.SessionId)] = cntx.SessionId ?? "NULL"
                // TODO: modul nev
            });

            return callNext();
        }
    }
}
