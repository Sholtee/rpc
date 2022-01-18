/********************************************************************************
* WebServiceBuilder.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

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
        /// The default request handler.
        /// </summary>
        protected sealed class DefaultHandler : IRequestHandler
        {
            /// <inheritdoc/>
            public IRequestHandler? Next { get; }

            /// <summary>
            /// Closes the current session.
            /// </summary>
            public Task HandleAsync(RequestContext context)
            {
                if (context is null)
                    throw new ArgumentNullException(nameof(context));

                //
                // Ha csak valamelyik Handler at nem allitotta akkor HTTP 200 lesz a visszateresi kod.
                //

                context.Response.Close();
                return Task.CompletedTask;
            }
        }

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
        /// The URL on which the service will listen.
        /// </summary>
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "HttpListener accepts non-standard URLs (see '+' & '*') too.")]
        public string Url { get; set; } = "http://localhost:1986";

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
        /// Builds a new <see cref="WebService"/> instance.
        /// </summary>
        public virtual WebService Build() => new WebService(Url, ServiceCollection);

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