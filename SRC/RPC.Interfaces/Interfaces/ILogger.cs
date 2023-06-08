/********************************************************************************
* ILog.cs                                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Defines logging severity levels.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Logging is disabled
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Los unrecoverabled failures.
        /// </summary>
        Critical = 1,

        /// <summary>
        /// Logs recoverably errors.
        /// </summary>
        Error = 2,

        /// <summary>
        /// Logs unexpected events.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Logs containing general informations-
        /// </summary>
        Information = 4,

        /// <summary>
        /// Logs containing debug informations. 
        /// </summary>
        Debug = 5,

        /// <summary>
        /// Logs that contain the most detailed messages
        /// </summary>
        Trace = 6
    }

    /// <summary>
    /// Defines the contract of loggers.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Writes a log entry.
        /// </summary>
        void Log(LogLevel logLevel, string eventId, string message, object? state, Exception? exception);

        /// <summary>
        /// Gets or sets the maximum level to be logged
        /// </summary>
        LogLevel Level { get; set; }
    }

    /// <summary>
    /// Defines some extensions over the <see cref="ILogger"/> interface.
    /// </summary>
    public static class ILoggerExtensions
    {
        /// <summary>
        /// Trace.
        /// </summary>
        public static void Trace(this ILogger logger, string eventId, string message, object? state = null)
        {
            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            logger.Log(LogLevel.Trace, eventId, message, state, exception: null);
        }

        /// <summary>
        /// Debug.
        /// </summary>
        public static void Debug(this ILogger logger, string eventId, string message, object? state = null)
        {
            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            logger.Log(LogLevel.Debug, eventId, message, state, exception: null);
        }

        /// <summary>
        /// Info.
        /// </summary>
        public static void Info(this ILogger logger, string eventId, string message, object? state = null)
        {
            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            logger.Log(LogLevel.Information, eventId, message, state, exception: null);
        }

        /// <summary>
        /// Warning.
        /// </summary>
        public static void Warning(this ILogger logger, string eventId, string message, object? state = null, Exception? exception = null)
        {
            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            logger.Log(LogLevel.Warning, eventId, message, state, exception);
        }

        /// <summary>
        /// Error.
        /// </summary>
        public static void Error(this ILogger logger, string eventId, string message, object? state = null, Exception? exception = null)
        {
            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            logger.Log(LogLevel.Error, eventId, message, state, exception);
        }

        /// <summary>
        /// Critical.
        /// </summary>
        public static void Critical(this ILogger logger, string eventId, string message, object? state = null, Exception? exception = null)
        {
            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            logger.Log(LogLevel.Critical, eventId, message, state, exception);
        }
    }
}
