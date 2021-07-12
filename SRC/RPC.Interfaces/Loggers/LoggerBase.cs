/********************************************************************************
* LoggerBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Defines an abstract logger.
    /// </summary>
    public abstract class LoggerBase
    {
        /// <summary>
        /// Logger implementation.
        /// </summary>
        public abstract object? Invoke(LogContext context, Func<object?> callNext);
    }
}
