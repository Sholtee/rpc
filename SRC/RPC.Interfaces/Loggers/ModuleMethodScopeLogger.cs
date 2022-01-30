/********************************************************************************
* ModuleMethodScopeLogger.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            IRpcRequestContext cntx = context.Scope.Get<IRpcRequestContext>();

            IDisposable logScope = context.Logger.BeginScope(new Dictionary<string, object>
            {
                [nameof(cntx.Module)]    = cntx.Module,
                [nameof(cntx.Method)]    = cntx.Method,
                [nameof(cntx.SessionId)] = cntx.SessionId ?? "NULL"
            });

            if (typeof(Task).IsAssignableFrom(context.Method.ReturnType))
            {
                Task task = (Task) callNext()!;
                task.ContinueWith(_ => logScope.Dispose(), default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return task;
            }

            try
            {
                return callNext();
            }
            finally
            {
                logScope.Dispose();
            }
        }
    }
}
