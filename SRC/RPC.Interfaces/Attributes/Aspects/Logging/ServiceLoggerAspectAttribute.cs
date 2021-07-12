/********************************************************************************
* ServiceLoggerAspectAttribute.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Marks a service to be logged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class ServiceLoggerAspectAttribute: LoggerAspectAttribute
    {
        /// <summary>
        /// Creates a new <see cref="ServiceLoggerAspectAttribute"/> instance.
        /// </summary>
        public ServiceLoggerAspectAttribute() : base(typeof(ServiceMethodScopeLogger), typeof(ExceptionLogger), typeof(ParameterLogger)) { }
    }
}
