/********************************************************************************
* WebServiceBuilder.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

#pragma warning disable CA1033 // Interface methods should be callable by child types

namespace Solti.Utils.Rpc
{
    using DI;
    using DI.Interfaces;
    using Interfaces;
    using Pipeline;

    /// <summary>
    /// Builds <see cref="WebService"/> instances.
    /// </summary>
    public class WebServiceBuilder : IRequestPipeConfigurator<RequestHandlerFactory>
    {
        /// <summary>
        /// The <see cref="IServiceCollection"/> containing all the necessary service to build a <see cref="WebService"/> instance.
        /// </summary>
        protected internal IServiceCollection ServiceCollection { get; }

        /// <summary>
        /// The <see cref="AbstractServiceEntry"/> containing the pipe definition.
        /// </summary>
        protected internal AbstractServiceEntry Pipe { get; }

        /// <summary>
        /// Creates a new <see cref="WebServiceBuilder"/> instance.
        /// </summary>
        public WebServiceBuilder()
        {
            ServiceCollection = new ServiceCollection();
            Pipe = ServiceCollection
                .Service<IRequestHandler, DefaultHandler>(Lifetime.Scoped)
                .LastEntry;
        }

        /// <summary>
        /// Configures the required services.
        /// </summary>
        public WebServiceBuilder ConfigureServices(Action<IServiceCollection> configCallback)
        {
            if (configCallback is null)
                throw new ArgumentNullException(nameof(configCallback));

            configCallback(ServiceCollection);
            return this;
        }

        /// <summary>
        /// Configures the request pipeline.
        /// </summary>
        public WebServiceBuilder ConfigurePipeline(Action<IRequestPipeConfigurator<RequestHandlerFactory>> configCallback)
        {
            if (configCallback is null)
                throw new ArgumentNullException(nameof(configCallback));
            configCallback(this);
            return this;
        }

        /// <summary>
        /// Configures the backend implementation.
        /// </summary>
        public WebServiceBuilder ConfigureBackend(Func<IInjector, IHttpServer> factory) => ConfigureServices(svcs => svcs.Factory<IHttpServer>(factory, Lifetime.Singleton));

        /// <summary>
        /// Builds a new <see cref="WebService"/> instance.
        /// </summary>
        public virtual WebService Build() => new WebService(ServiceCollection);

        IRequestPipeConfigurator<RequestHandlerFactory> IRequestPipeConfigurator<RequestHandlerFactory>.Use<TRequestHandlerFactory>(Action<TRequestHandlerFactory>? configCallback)
        {
            TRequestHandlerFactory factory = new()
            {
                WebServiceBuilder = this
            };
            configCallback?.Invoke(factory);
            factory.FinishConfiguration();
            return this;
        }
    }
}