/********************************************************************************
* LoggerBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;

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

            try
            {
                return callNext();
            }
            finally
            {
                context.Logger.LogInformation(Trace.TIME_ELAPSED, stopWatch.ElapsedMilliseconds);

                stopWatch.Stop();
            }
        }
    }
}
