/********************************************************************************
* ConsoleLogger.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Server.Sample
{
    using Internals;
    
    internal class ConsoleLogger : LoggerBase 
    {
        protected override void LogCore(string message) => Console.Out.WriteLine(message);

        public static ILogger Create<TCategory>() => new ConsoleLogger(GetDefaultCategory<TCategory>());

        public ConsoleLogger(string category) : base(category) { }
    }
}
