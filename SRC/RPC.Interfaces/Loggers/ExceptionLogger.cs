/********************************************************************************
* LoggerBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Interfaces
{
    using Properties;

    /// <summary>
    /// Logs the unhandled exceptions.
    /// </summary>
    public sealed class ExceptionLogger : LoggerBase
    {
        /// <inheritdoc/>
        public override object? Invoke(LogContext context, Func<object?> callNext)
        {
            try
            {
                return callNext();
            }
            catch (Exception ex)
            {
                context.Logger.LogError(string.Format(Trace.Culture, Trace.UNHANDLED_EXCEPTION, ex));
                throw;
            }
        }
    }
}
