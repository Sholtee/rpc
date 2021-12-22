/********************************************************************************
* ExceptionLogger.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Interfaces
{
    using Properties;

    /// <summary>
    /// Logs the unhandled exceptions.
    /// </summary>
    public sealed class ExceptionLogger: LoggerBase
    {
        /// <inheritdoc/>
        public override object? Invoke(LogContext context, Func<object?> callNext)
        {
            if (typeof(Task).IsAssignableFrom(context.Method.ReturnType))
            {
                Task task = (Task) callNext()!;
                task.ContinueWith
                (
                    ex => context.Logger.LogError(Trace.UNHANDLED_EXCEPTION, ex.Exception.InnerException),
                    default,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default
                );
                return task;
            }

            try
            {
                return callNext();
            }
            catch (Exception ex)
            {
                context.Logger.LogError(Trace.UNHANDLED_EXCEPTION, ex);
                throw;
            }
        }
    }
}
