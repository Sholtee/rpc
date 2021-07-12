/********************************************************************************
* LoggerBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Interfaces
{
    using Properties;

    /// <summary>
    /// Logs the invocation parameters.
    /// </summary>
    public sealed class ParameterLogger : LoggerBase
    {
        /// <inheritdoc/>
        public override object? Invoke(LogContext context, Func<object?> callNext)
        {
            context.Logger.LogInformation
            (
                Trace.PARAMZ,
                string.Join($",{Environment.NewLine}", context
                    .Method
                    .GetParameters()
                    .Select((para, i) => $"{para.Name}:{context.Args[i]?.ToString() ?? "NULL"}"))
            );
            return callNext();
        }
    }
}
