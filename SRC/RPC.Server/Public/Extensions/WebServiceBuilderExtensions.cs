/********************************************************************************
* WebServiceBuilderExtensions.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

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
                .ConfigureServices(static (svcs, lifetimes) => svcs.Service<ILogger, TraceLogger>(lifetimes.Scoped()))
                .Build();
        }

        /// <summary>
        /// Defines a basic RPC service.
        /// </summary>
        /// <remarks>The defined RPC service uses: <see cref="ExceptionCatcher"/>, <see cref="RequestLimiter"/>, <see cref="HttpAccessControl"/>, <see cref="SchemaProvider"/>, <see cref="RequestTimeout"/> and <see cref="Modules"/>.</remarks>
        public static WebServiceBuilder ConfigureRpcService(this WebServiceBuilder webServiceBuilder, Action<RequestHandlerBuilder> configurator, bool useDefaultLogger = true)
        {
            if (webServiceBuilder is null)
                throw new ArgumentNullException(nameof(webServiceBuilder));

            if (configurator is null)
                throw new ArgumentNullException(nameof(configurator));

            webServiceBuilder
                .ConfigurePipeline(pipeline => pipeline
                    .Use<SessionProvider>()
                    .Use<Modules>(modules =>
                    {
                        modules.InstallBuiltInModules();
                        configurator(modules);
                    })
                    .Use<RequestTimeout>(configurator)
                    .Use<SchemaProvider>(sp => 
                    {
                        Modules modules = sp.GetParent<Modules>()!;

                        foreach (Type module in modules.RegisteredModules)
                        {
                            if (module.GetCustomAttribute<PublishSchemaAttribute>() is not null)
                                sp.Register(module);
                        }

                        //
                        // Itt a "configurator" tudatosan nincs hivva, mert a PublishSchemaAttribute-al publikalunk
                        //
                    })
                    .Use<HttpAccessControl>(hac => 
                    {
                        hac.AllowedMethods.Add("POST"); // module invocation
                        hac.AllowedMethods.Add("GET"); // schema query
                        hac.AllowedHeaders.Add("Content-Type");
                        hac.AllowedHeaders.Add("Content-Length");

                        configurator(hac);
                    })
                    .Use<RequestLimiter>(configurator)
                    .Use<ExceptionCatcher>(configurator));

            if (useDefaultLogger)
                webServiceBuilder.ConfigureServices(static (svcs, lifetime) => svcs.Service<ILogger, TraceLogger>(lifetime.Scoped()));

            return webServiceBuilder;
        }
    }
}