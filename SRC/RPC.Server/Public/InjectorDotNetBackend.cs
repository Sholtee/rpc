/********************************************************************************
* InjectorDotNetBackend.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
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
        private sealed class InjectorDotNetLifetimes : Lifetimes
        {
            private static readonly IReadOnlyDictionary<string, LifetimeBase> FAvailableLifetimes = new Dictionary<string, LifetimeBase>
            {
                { nameof(Lifetime.Singleton), Lifetime.Singleton },
                { nameof(Lifetime.Scoped),    Lifetime.Scoped },
                { nameof(Lifetime.Transient), Lifetime.Transient },
                { nameof(Lifetime.Pooled),    Lifetime.Pooled },
            };

            public override LifetimeBase this[string name] => FAvailableLifetimes[name];
        }

        /// <inheritdoc/>
        public IServiceCollection Services { get; } = ServiceCollection.Create();

        /// <inheritdoc/>
        public Lifetimes Lifetimes { get; } = new InjectorDotNetLifetimes();

        /// <inheritdoc/>
        public IScopeFactory CreateFactory(CancellationToken cancellation) => ScopeFactory.Create(Services);
    }
}