/********************************************************************************
* IServiceDescriptor.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Exposes the description of a RPC service.
    /// </summary>
    public interface IServiceDescriptor
    {
        /// <summary>
        /// The name of the RPC service.
        /// </summary>
        Task<string> Name { get; }

        /// <summary>
        /// The version of the RPC service.
        /// </summary>
        Task<Version> Version { get; }
    }
}
