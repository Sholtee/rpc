/********************************************************************************
* VoidLogger.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Server.Sample
{
    using Primitives.Patterns;

    internal sealed class VoidLogger: Singleton<VoidLogger>, ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => new Disposable();

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }
    }
}
