/********************************************************************************
* IServiceDescriptor.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
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
        string Name { get; }

        /// <summary>
        /// The version of the RPC service.
        /// </summary>
        Version Version { get; }
    }
}
