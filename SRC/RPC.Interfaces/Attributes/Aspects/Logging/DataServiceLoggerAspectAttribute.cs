/********************************************************************************
* DataServiceLoggerAspectAttribute.cs                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Marks a data-service to be logged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class DataServiceLoggerAspectAttribute : LoggerAspectAttribute
    {
        /// <summary>
        /// Creates a new <see cref="ServiceLoggerAspectAttribute"/> instance.
        /// </summary>
        public DataServiceLoggerAspectAttribute() : base(typeof(ServiceMethodScopeLogger), typeof(ExceptionLogger), typeof(ParameterLogger), typeof(StopWatchLogger)) { }
    }
}
