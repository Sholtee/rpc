/********************************************************************************
* DiProvider.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading;

namespace Solti.Utils.Rpc
{
    using DI;
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// The default <see cref="IDiProvider"/> implementation
    /// </summary>
    public class DiProvider : IDiProvider
    {
        /// <summary>
        /// The configuration applied on each created scope.
        /// </summary>
        public ScopeOptions ScopeOptions { get; set; } = new();

        /// <inheritdoc/>
        public IServiceCollection Services { get; } = new ServiceCollection();

        /// <inheritdoc/>
        public IScopeFactory CreateFactory(CancellationToken cancellation) => ScopeFactory.Create(Services, ScopeOptions, cancellation);
    }
}