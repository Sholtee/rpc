/********************************************************************************
* StopWatchLogger.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Interfaces
{
    using Properties;

    /// <summary>
    /// Logs the invocation duration.
    /// </summary>
    public sealed class StopWatchLogger: LoggerBase
    {
        /// <inheritdoc/>
        public override object? Invoke(LogContext context, Func<object?> callNext)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            if (typeof(Task).IsAssignableFrom(context.Method.ReturnType))
            {
                Task task = (Task) callNext()!;
                task.ContinueWith
                (
                    _ => LogElapsedTime(),
                    default,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
                return task;
            }
     
            try
            {
                return callNext();
            }
            finally
            {
                LogElapsedTime();
            }

            void LogElapsedTime()
            {
                stopWatch.Stop();
                context.Logger.LogInformation(Trace.TIME_ELAPSED, stopWatch.ElapsedMilliseconds);
            }
        }
    }
}
