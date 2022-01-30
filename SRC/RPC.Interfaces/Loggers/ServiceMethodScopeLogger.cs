/********************************************************************************
* ServiceMethodScopeLogger.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            IDisposable logScope = context.Logger.BeginScope(new Dictionary<string, object>
            {
                ["Service"] = context.Method.ReflectedType,
                ["Method"] = context.Method
                // TODO: szerviz nev
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
