/********************************************************************************
* TraceLogger.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Internals
{
    internal class TraceLogger : LoggerBase 
    {
        protected override void LogCore(string message) => Trace.WriteLine(message);

        public static ILogger Create<TCategory>() => new TraceLogger(GetDefaultCategory<TCategory>());

        public TraceLogger(string category) : base(category) { }
    }
}
