/********************************************************************************
* IHostRunnerFactory.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes a host runner factory.
    /// </summary>
    public interface IHostRunnerFactory
    {
        /// <summary>
        /// Returns true if the factory should be used.
        /// </summary>
        bool IsCompatible(IHost host);

        /// <summary>
        /// Creates a new runner for the given <see cref="IHost"/>.
        /// </summary>
        IHostRunner CreateRunner(IHost host, HostConfiguration configuration);
    }
}
