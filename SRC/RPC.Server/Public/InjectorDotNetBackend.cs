/********************************************************************************
* InjectorDotNetBackend.cs                                                      *
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
    /// The default <see cref="IDiProvider"/> implementation using the <see cref="ServiceCollection"/> and <see cref="ScopeFactory"/> classes.
    /// </summary>
    public class InjectorDotNetBackend : IDiProvider
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