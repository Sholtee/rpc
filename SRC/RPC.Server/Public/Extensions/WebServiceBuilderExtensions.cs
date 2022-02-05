/********************************************************************************
* WebServiceBuilderExtensions.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Microsoft.Extensions.Logging;

#pragma warning disable CA1054 // URI-like parameters should not be strings

namespace Solti.Utils.Rpc
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Pipeline;
    using Servers;

    /// <summary>
    /// Defines some handy extensons to the <see cref="WebServiceBuilder"/> class.
    /// </summary>
    public static class WebServiceBuilderExtensions
    {
        /// <summary>
        /// Builds a minimal web service.
        /// </summary>
        public static WebService BuildMinimalService(this WebServiceBuilder webServiceBuilder, string url = "http://localhost:1986")
        {
            if (webServiceBuilder is null)
                throw new ArgumentNullException(nameof(webServiceBuilder));

            return webServiceBuilder
                .ConfigureBackend(_ => new HttpListenerBackend(url) { ReserveUrl = true })
                .ConfigureServices(svcs => svcs.Factory<ILogger>(i => TraceLogger.Create<WebService>(), Lifetime.Scoped))
                .Build();
        }

        /// <summary>
        /// Defines a basic RPC service.
        /// </summary>
        /// <remarks>The defined RPC service uses: <see cref="ExceptionCatcher"/>, <see cref="RequestLimiter"/>, <see cref="RpcAccessControl"/>, <see cref="RequestTimeout"/> and <see cref="Modules"/>.</remarks>
        public static WebServiceBuilder ConfigureRpcService(this WebServiceBuilder webServiceBuilder, Action<RequestHandlerBuilder> configurator, bool useDefaultLogger = true)
        {
            if (webServiceBuilder is null)
                throw new ArgumentNullException(nameof(webServiceBuilder));

            if (configurator is null)
                throw new ArgumentNullException(nameof(configurator));

            webServiceBuilder
                .ConfigurePipeline(pipeline => pipeline
                    .Use<Modules>(modules =>
                    {
                        modules.InstallBuiltInModules();
                        configurator(modules);
                    })
                    .Use<RequestTimeout>(configurator)
                    .Use<SchemaProvider>(sp => 
                    {
                        sp.Register<IServiceDescriptor>();
                        configurator(sp);
                    })
                    .Use<RpcAccessControl>(configurator)
                    .Use<RequestLimiter>(configurator)
                    .Use<ExceptionCatcher>(configurator));

            if (useDefaultLogger)
                webServiceBuilder.ConfigureServices(services => services.Factory<ILogger>(_ => TraceLogger.Create<WebService>(), Lifetime.Scoped));

            return webServiceBuilder;
        }
    }
}