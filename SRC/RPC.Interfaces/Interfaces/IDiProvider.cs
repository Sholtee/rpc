/********************************************************************************
* IDiProvider.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Specifies the contract for dependency injection.
    /// </summary>
    public interface IDiProvider
    {
        /// <summary>
        /// Services from which the scopes will be created.
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// Creates a new <see cref="IScopeFactory"/> instance against the given <see cref="Services"/>.
        /// </summary>
        IScopeFactory CreateFactory(CancellationToken cancellation = default);
    }
}
