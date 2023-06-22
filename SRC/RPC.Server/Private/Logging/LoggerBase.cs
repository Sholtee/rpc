/********************************************************************************
* LoggerBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;
    using Primitives.Patterns;

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
        /// The concrete logger invocation.
        /// </summary>
        /// <param name="message"></param>
        protected abstract void LogCore(string message);

        /// <summary>
        /// The actual state.
        /// </summary>
        protected Stack<IDictionary<string, object?>> Scopes { get; } = new();

        /// <inheritdoc/>
        public LogLevel Level { get; set; }

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
                        Level = logLevel,
                        EventId = eventId ?? throw new ArgumentNullException(nameof(eventId)),
                        Message = message ?? throw new ArgumentNullException(nameof(message)),
                        State = Scopes.Reverse().Aggregate
                        (
                            state is not null ? AsDictionary(state) : new Dictionary<string, object?>(),
                            static (accu, current) =>
                            {
                                foreach (KeyValuePair<string, object?> kvp in current)
                                {
                                    accu[kvp.Key] = kvp.Value;
                                }
                                return accu;
                            }
                        ),
                        Exception = exception
                    },
                    options
                )
            );
        }

        private static readonly ConcurrentDictionary<Type, Func<object, IDictionary<string, object?>>> FConverterCache = new();

        private sealed class ScopeLifetime : Disposable
        {
            private Stack<IDictionary<string, object?>> Scopes { get; }

            public ScopeLifetime(Stack<IDictionary<string, object?>> scopes) => Scopes = scopes;

            protected override void Dispose(bool disposeManaged)
            {
                if (disposeManaged)
                    Scopes.Pop();

                base.Dispose(disposeManaged);
            }
        }

        /// <inheritdoc/>
        public IDisposable BeginScope(object state)
        {
            if (state is null)
                throw new ArgumentNullException(nameof(state));

            Scopes.Push
            (
                AsDictionary(state)
            );

            return new ScopeLifetime(Scopes);
        }

        private static IDictionary<string, object?> AsDictionary(object state)
        {
            return state as IDictionary<string, object?> ?? FConverterCache.GetOrAdd(state.GetType(), ConverterFactory).Invoke(state);

            static Func<object, IDictionary<string, object?>> ConverterFactory(Type type)
            {
                ParameterExpression
                    state = Expression.Parameter(typeof(object), nameof(state)),
                    converted = Expression.Variable(type, nameof(converted)),
                    result = Expression.Variable(typeof(IDictionary<string, object?>), nameof(result));

                BlockExpression block = Expression.Block
                (
                    type: typeof(IDictionary<string, object?>),
                    variables: new[] { result, converted },
                    expressions: new Expression[]
                    {
                        Expression.Assign(result, Expression.New(typeof(Dictionary<string, object>))),
                        Expression.Assign(converted, Expression.Convert(state, type))
                    }
                    .Concat
                    (
                       type
                            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                            .Where(static prop => prop.CanRead)
                            .Select
                            (
                                prop => Expression.Invoke
                                (
                                    Expression.Constant((Action<IDictionary<string, object?>, string, object?>)AddValue),
                                    result,
                                    Expression.Constant(prop.Name),
                                    Expression.Convert
                                    (
                                        Expression.Property(converted, prop),
                                        typeof(object)
                                    )
                                )
                            )
                    )
                    .Append(result)
                );

                return Expression.Lambda<Func<object, IDictionary<string, object?>>>(body: block, state).Compile();

                static void AddValue(IDictionary<string, object?> result, string name, object? value) => result[name] = value;
            }
        }
    }
}
