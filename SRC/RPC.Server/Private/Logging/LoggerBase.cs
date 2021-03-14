/********************************************************************************
* LoggerBase.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Internals
{
    using Primitives.Patterns;

    /// <summary>
    /// Minimalist logger implementation, intended for private use only.
    /// </summary>
    public abstract class LoggerBase : ILogger
    {
        private readonly Stack<string> FScopes = new Stack<string>();

        private sealed class Popper : Disposable
        {
            public string State { get; }

            public Stack<string> Scopes { get; }

            public Popper(Stack<string> scopes)
            {
                Scopes = scopes;
                State = scopes.Peek();
            }

            [SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose")]
            protected override void Dispose(bool disposeManaged)
            {
                CheckNotDisposed();

                //
                // A szulo hatokor nem szabadithato elobb fol mint a gyermek hatokor
                //

                if (Scopes.Peek() != State)
                    throw new InvalidOperationException();

                Scopes.Pop();
            }
        }

        /// <summary>
        /// Gets the default category.
        /// </summary>
        protected static string GetDefaultCategory<TCategory>() => $"'{Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName)}' ({typeof(TCategory).Name})";

        /// <summary>
        /// Creates a new <see cref="LoggerBase"/> instance.
        /// </summary>
        protected LoggerBase(string category) => Category = category;

        /// <summary>
        /// The concrete logger invocation.
        /// </summary>
        /// <param name="message"></param>
        protected abstract void LogCore(string message);

        /// <summary>
        /// See category.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// See <see cref="ILogger.BeginScope{TState}(TState)"/>.
        /// </summary>
        public virtual IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            string scope = state is IDictionary<string, object> dict
                ? $"[{string.Join(", ", dict.Select(kv => $"{kv.Key ?? string.Empty} = {kv.Value ?? "NULL"}"))}]"
                : state.ToString();

            FScopes.Push(scope);
            return new Popper(FScopes);
        }

        /// <summary>
        /// See <see cref="ILogger.IsEnabled(LogLevel)"/>.
        /// </summary>
        #pragma warning disable CS3001 // LogLevel is not CLS-compliant
        public bool IsEnabled(LogLevel logLevel) => true;
        #pragma warning restore CS3001

        /// <summary>
        /// See <see cref="ILogger.Log{TState}(LogLevel, EventId, TState, Exception, Func{TState, Exception, string})"/>.
        /// </summary>
        #pragma warning disable CS3001 // LogLevel & EventId are not CLS-compliant
        public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        #pragma warning restore CS3001
        {
            if (!IsEnabled(logLevel)) return;

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message)) return;
            if (exception != null) message += $" -> {Environment.NewLine}{exception}";

            message = $"{Category}: {string.Join(" ", FScopes.Reverse())} { logLevel }: {message}";

            LogCore(message);
        }
    }
}
