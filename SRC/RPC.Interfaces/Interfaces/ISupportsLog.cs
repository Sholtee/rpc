/********************************************************************************
* ISupportsLog.cs                                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Indicates that a service supports logging.
    /// </summary>
    public interface ISupportsLog
    {
        /// <summary>
        /// Allows logging if the <see cref="ILogger"/> service is accessible.
        /// </summary>
        bool AllowLogs { get; set; }
    }
}
