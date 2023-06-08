/********************************************************************************
* LoggerBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    /// <summary>
    /// Minimalist logger implementation, meant to dump log streams containing parseable JSON data
    /// </summary>
    /// <remarks>Every request should have its own logger instance.</remarks>
    public abstract class LoggerBase : ILogger
    {
        private sealed class LogEntry
        {
            public required LogLevel Level { get; init; }

            public required string EventId { get; init; }

            public required string Message { get; init; }

            public required string Origin { get; init; }

            public DateTime TimeStampUtc { get; } = DateTime.UtcNow;

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public Guid? RequestId { get; init; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public object? State { get; init; }

            [JsonIgnore]
            public Exception? Exception { get; init; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyName(nameof(Exception))]
            public string? Error => Exception?.Message;

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? StackTrace => Exception?.StackTrace;
        }

        /// <summary>
        /// Creates a new <see cref="LoggerBase"/> instance.
        /// </summary>
        protected LoggerBase(IHttpRequest? request) => Request = request;

        /// <summary>
        /// The concrete logger invocation.
        /// </summary>
        /// <param name="message"></param>
        protected abstract void LogCore(string message);

        /// <summary>
        /// The associated request.
        /// </summary>
        public IHttpRequest? Request { get; }

        /// <inheritdoc/>
        public LogLevel Level { get; set; }

        /// <summary>
        /// The origin of dumped log entries.
        /// </summary>
        public string Origin { get; set; } = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);

        /// <inheritdoc/>
        public virtual void Log(LogLevel logLevel, string eventId, string message, object? state, Exception? exception)
        {
            if (logLevel > Level)
                return;

            JsonSerializerOptions options = new()
            {
                WriteIndented = false
            };
            options.Converters.Add(new TypeConverter());

            LogCore
            (
                JsonSerializer.Serialize
                (
                    new LogEntry
                    {
                        Level     = logLevel,
                        EventId   = eventId ?? throw new ArgumentNullException(nameof(eventId)),
                        Message   = message ?? throw new ArgumentNullException(nameof(message)),
                        State     = state,
                        Origin    = Origin,
                        RequestId = Request?.Id,
                        Exception = exception
                    },
                    options
                )
            );
        }
    }
}
