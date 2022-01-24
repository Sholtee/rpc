/********************************************************************************
* WebServiceBuilder.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Solti.Utils.Rpc
{
    using DI;
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// Builds <see cref="WebService"/> instances.
    /// </summary>
    public class WebServiceBuilder : IBuilder<WebService>.IParameterizedBuilder<CancellationToken>
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
        public WebServiceBuilder ConfigurePipeline(Action<IRequestPipeConfigurator> configCallback)
        {
            if (configCallback is null)
                throw new ArgumentNullException(nameof(configCallback));

            configCallback(new RequestPipeConfigurator(this));
            return this;
        }

        /// <summary>
        /// Configures the backend implementation.
        /// </summary>
        public WebServiceBuilder ConfigureBackend(Func<IInjector, IHttpServer> factory) => ConfigureServices(svcs => svcs.Factory<IHttpServer>(factory, Lifetime.Singleton));

        /// <summary>
        /// Builds a new <see cref="WebService"/> instance.
        /// </summary>
        [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration")]
        public virtual WebService Build(CancellationToken cancellation = default) => new WebService(ServiceCollection, null, cancellation); // TODO: ScopeOptions is be lehesssen allitani

        private sealed class RequestPipeConfigurator : IRequestPipeConfigurator
        {
            public WebServiceBuilder WebServiceBuilder { get; }

            public RequestPipeConfigurator(WebServiceBuilder webServiceBuilder) => WebServiceBuilder = webServiceBuilder;

            IRequestPipeConfigurator IRequestPipeConfigurator.Use<TRequestHandlerBuilder>(Action<TRequestHandlerBuilder>? configCallback)
            {
                //
                // Mivel minden egyes builder-re csak egyszer lesz meghivva ezert pont jo
                // az Activator-os peldanyositas is
                //

                TRequestHandlerBuilder factory = (TRequestHandlerBuilder) Activator.CreateInstance(typeof(TRequestHandlerBuilder), WebServiceBuilder);
                configCallback?.Invoke(factory);
                return this;
            }
        }
    }
}