/********************************************************************************
* TraceLogger.cs                                                                *
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

    internal class TraceLogger : ILogger
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

        public static ILogger Create<TCategory>() => new TraceLogger($"'{Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName)}' ({typeof(TCategory).Name})");

        public TraceLogger(string category) => Category = category;

        public string Category { get; }

        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            string scope = state is IDictionary<string, object> dict
                ? $"[{string.Join(", ", dict.Select(kv => $"{kv.Key} = {kv.Value}").ToArray())}]"
                : state.ToString();

            FScopes.Push(scope);
            return new Popper(FScopes);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message)) return;
            if (exception != null) message += $" -> {Environment.NewLine}{exception}";

            message = $"{Category}: {string.Join(" ", FScopes.Reverse())} { logLevel }: {message}";

            Trace.WriteLine(message);
        }
    }
}
