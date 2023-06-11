/********************************************************************************
* TraceLogger.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    internal class TraceLogger : LoggerBase 
    {
        protected override void LogCore(string message) => Trace.WriteLine(message);

        public TraceLogger(IHttpRequest? request) : base(request) { }
    }
}
