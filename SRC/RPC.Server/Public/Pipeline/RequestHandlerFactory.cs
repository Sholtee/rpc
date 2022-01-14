/********************************************************************************
* RequestHandlerFactory.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// Request handler factory.
    /// </summary>
    public abstract class RequestHandlerFactory
    {
        /// <summary>
        /// Registers the underlying request handler.
        /// </summary>
        public virtual void AddTo(IServiceCollection services)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            services
                .First(svc => svc.Interface == typeof(IRequestHandler) && svc.Name is null)
                .ApplyProxy((_, _, next) => Create((IRequestHandler) next));
        }

        /// <summary>
        /// Creates a new request handler instance.
        /// </summary>
        public abstract IRequestHandler Create(IRequestHandler next);
    }

    /// <summary>
    /// Defines extensions related to <see cref="RequestHandlerFactory"/> class.
    /// </summary>
    public static class RequestHandlerFactoryExtensions
    {
        /// <summary>
        /// Uses the given request handler.
        /// </summary>
        public static void Use<THandlerFactory>(this IServiceCollection services, Action<THandlerFactory> configCallback) where THandlerFactory : RequestHandlerFactory, new()
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            if (configCallback is null)
                throw new ArgumentNullException(nameof(configCallback));

            THandlerFactory factory = new();
            configCallback(factory);
            factory.AddTo(services);
        }

        /// <summary>
        /// Uses the given request handler.
        /// </summary>
        public static void Use<THandlerFactory>(this IServiceCollection services) where THandlerFactory : RequestHandlerFactory, new() => Use<THandlerFactory>(services, _ => { });
    }
}
