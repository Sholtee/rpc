/********************************************************************************
* WebServiceBuilderExtensions.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc
{
    using DI.Interfaces;
    using Internals;
    using Pipeline;

    /// <summary>
    /// Defines some handy extensons to the <see cref="WebServiceBuilder"/> class.
    /// </summary>
    public static class WebServiceBuilderExtensions
    {
        /// <summary>
        /// Builds a minimal web service.
        /// </summary>
        public static WebService BuildMinimalService(this WebServiceBuilder webServiceBuilder)
        {
            if (webServiceBuilder is null)
                throw new ArgumentNullException(nameof(webServiceBuilder));

            return webServiceBuilder
                .ConfigureServices(svcs => svcs.Factory<ILogger>(i => TraceLogger.Create<WebService>(), Lifetime.Singleton))
                .Build();
        }

        /// <summary>
        /// Defines a basic RPC service.
        /// </summary>
        /// <remarks>The defined RPC service uses: <see cref="ExceptionCatcher"/>, <see cref="RpcAccessControl"/>, <see cref="Timeout"/> and <see cref="Modules"/>.</remarks>
        public static WebServiceBuilder DefineRpcService(this WebServiceBuilder webServiceBuilder, Action<RequestHandlerFactory> configurator, bool useDefaultLogger = true)
        {
            if (webServiceBuilder is null)
                throw new ArgumentNullException(nameof(webServiceBuilder));

            if (configurator is null)
                throw new ArgumentNullException(nameof(configurator));

            webServiceBuilder
                .ConfigurePipeline(pipeline => pipeline
                    .Use<Modules>(registry =>
                    {
                        registry.InstallBuiltInModules();
                        configurator(registry);
                    })
                    .Use<Timeout>(configurator)
                    .Use<RpcAccessControl>(configurator)
                    .Use<RequestLimiter>(configurator)
                    .Use<ExceptionCatcher>(configurator));

            if (useDefaultLogger)
                webServiceBuilder.ConfigureServices(services => services.Factory<ILogger>(_ => TraceLogger.Create<WebService>(), Lifetime.Singleton));

            return webServiceBuilder;
        }
    }
}