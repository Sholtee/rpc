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
    /// 
    /// </summary>
    public abstract class Lifetimes
    {
        /// <summary>
        /// 
        /// </summary>
        public abstract LifetimeBase this[string name] { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public static class LifetimesExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public static LifetimeBase Singleton(this Lifetimes src) => src[nameof(Singleton)];

        /// <summary>
        /// 
        /// </summary>
        public static LifetimeBase Scoped(this Lifetimes src) => src[nameof(Scoped)];

        /// <summary>
        /// 
        /// </summary>
        public static LifetimeBase Transient(this Lifetimes src) => src[nameof(Transient)];

        /// <summary>
        /// 
        /// </summary>
        public static LifetimeBase Pooled(this Lifetimes src) => src[nameof(Pooled)];
    }

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
        /// The supported lifetimes.
        /// </summary>
        Lifetimes Lifetimes { get; }

        /// <summary>
        /// Creates a new <see cref="IScopeFactory"/> instance against the given <see cref="Services"/>.
        /// </summary>
        IScopeFactory CreateFactory(CancellationToken cancellation = default);
    }
}
