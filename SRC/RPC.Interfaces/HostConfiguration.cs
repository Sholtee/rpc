/********************************************************************************
* HostConfiguration.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes the configuration in which the host runs.
    /// </summary>
    public enum HostConfiguration
    {
        /// <summary>
        /// Debug
        /// </summary>
        Debug = 0,

        /// <summary>
        /// Release
        /// </summary>
        Release
    }
}
