/********************************************************************************
* ModuleLoggerAspectAttribute.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Marks a module to be logged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class ModuleLoggerAspectAttribute: LoggerAspectAttribute
    {
        /// <summary>
        /// Creates a new <see cref="ModuleLoggerAspectAttribute"/> instance.
        /// </summary>
        public ModuleLoggerAspectAttribute() : base(typeof(ModuleMethodScopeLogger), typeof(ExceptionLogger), typeof(ParameterLogger), typeof(StopWatchLogger)) { }
    }
}
