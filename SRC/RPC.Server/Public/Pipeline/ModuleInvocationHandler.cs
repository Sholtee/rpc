/********************************************************************************
* ModuleInvocationHandler.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Properties;

    /// <summary>
    /// Specifies the <see cref="ModuleInvocationHandler"/> configuration.
    /// </summary>
    public interface IModuleInvocationHandlerConfig
    {
        /// <summary>
        /// The context store.
        /// </summary>
        IDictionary<IInjector, IRpcRequestContext> ContextStore { get; }

        /// <summary>
        /// In runtime built delegate containing the module invocation logic.
        /// </summary>
        ModuleInvocation ModuleInvocation { get; }
    }

    /// <summary>
    /// Handles RPC module invocatios.
    /// </summary>
    public class ModuleInvocationHandler : RequestHandlerBase<IModuleInvocationHandlerConfig>
    {
        private sealed record RpcRequestContext
        (
            string? SessionId,
            string Module,
            string Method,
            IHttpRequest OriginalRequest,
            CancellationToken Cancellation
        ) : IRpcRequestContext
        {
            public Stream Payload => OriginalRequest.Payload;
        };

        /// <summary>
        /// Creates the HTTP response.
        /// </summary>
        /// <remarks>This operation cannot be cancelled.</remarks>
        protected virtual async Task CreateResponse(IInjector scope, IHttpResponse response, object? result)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (response is null)
                throw new ArgumentNullException(nameof(response));

            switch (result)
            {
                case Stream stream:
                    try
                    {
                        response.Headers["Content-Type"] = "application/octet-stream";
                        stream.Seek(0, SeekOrigin.Begin);
                        await stream.CopyToAsync(response.Payload);
                    }
                    finally
                    {
#if NETSTANDARD2_1_OR_GREATER
                        await stream.DisposeAsync();
#else
                        stream.Dispose();
#endif
                    }
                    break;
                case Exception ex:
                    response.Headers["Content-Type"] = "application/json";

                    Dictionary<string, string> data = new(ex.Data.Count);
                    foreach (DictionaryEntry entry in ex.Data)
                    {
                        data[entry.Key.ToString()] = entry.Value?.ToString() ?? string.Empty;
                    }

                    await scope.Get<IJsonSerializer>().SerializeAsync
                    (
                        typeof(RpcResponse),
                        new RpcResponse
                        {
                            Exception = new ExceptionInfo
                            {
                                TypeName = ex.GetType().AssemblyQualifiedName,
                                Message  = ex.Message,
                                Data     = data
                            }
                        },
                        response.Payload
                    );
                    break;
                default:
                    response.Headers["Content-Type"] = "application/json";
                    await scope.Get<IJsonSerializer>().SerializeAsync
                    (
                        typeof(RpcResponse),
                        new RpcResponse 
                        {
                            Result = result
                        }, 
                        response.Payload
                    );
                    break;
            }
        }

        /// <summary>
        /// Parses the <paramref name="context"/> to a RPC context.
        /// </summary>
        protected virtual IRpcRequestContext CreateContext(IHttpSession context, in CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            IHttpRequest request = context.Request;

            //
            // Metodus validalasa (POST eseten a keresnek kene legyen torzse).
            //

            if (request.Method.ToUpperInvariant() is not "POST")
                throw new HttpException(Errors.HTTP_METHOD_NOT_SUPPORTED) { Status = HttpStatusCode.MethodNotAllowed };

            //
            // Tartalom validalasa.
            //

            if (request.Headers["Content-Type"]?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) is not true)
                throw new HttpException(Errors.HTTP_CONTENT_NOT_SUPPORTED) { Status = HttpStatusCode.UnsupportedMediaType };

            IReadOnlyDictionary<string, string> paramz = request.QueryParameters;

            //
            // Szukseges parameterek lekerdezese (nem kis-nagy betu erzekeny).
            //

            return new RpcRequestContext
            (
                paramz[nameof(IRpcRequestContext.SessionId)],
                paramz[nameof(IRpcRequestContext.Module)] ?? throw new HttpException(Errors.NO_MODULE) { Status = HttpStatusCode.BadRequest },
                paramz[nameof(IRpcRequestContext.Method)] ?? throw new HttpException(Errors.NO_METHOD) { Status = HttpStatusCode.BadRequest },
                request,
                cancellation           
            );
        }

        /// <summary>
        /// Creates a new <see cref="ModuleInvocationHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public ModuleInvocationHandler(IRequestHandler next, IModuleInvocationHandlerConfig config): base(next, config) { }

        //
        // A handler-ben nem kell semmit sem naplozni mert az itt mar oldhato aspektusokkal.
        //

        /// <inheritdoc/>
        public override async Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            IRpcRequestContext rpcRequestContext = Config.ContextStore[scope] = CreateContext(context, cancellation);
            object? result;

            try
            {
                result = await Config.ModuleInvocation(scope, rpcRequestContext);
            }

            catch (Exception ex)
            {
                //
                // Egyedi HTTP hibakod is megadhato, azt nem szerializaljuk.
                //

                if (ex is HttpException) throw;

                //
                // Kulonben valid valasz fogja tartalmazni a hibat.
                //

                result = ex;
            }

            //
            // Mar nincs szukseg a kontextusra
            //

            finally 
            {
                Config.ContextStore.Remove(scope);
            }

            //
            // A valasz kiirasat mar nem lehet megszakitani.
            //

            await CreateResponse(scope, context.Response, result);
            await Next.HandleAsync(scope, context, cancellation);
        }
    }

    /// <summary>
    /// Configures services to be accessible via Remote Procedure Call.
    /// </summary>
    public class Modules : RequestHandlerBuilder, IModuleInvocationHandlerConfig
    {
        private ModuleInvocationBuilder ModuleInvocationBuilder { get; } = new();

        //
        // Az egyes scope-okhoz tartozo kontextust nem lehet ThreadLocal-ban tarolni:
        //
        //   threadLocal = context;
        //   await someAsyncTask();
        //   threadLocal == null;
        //

        /// <inheritdoc/>
        public IDictionary<IInjector, IRpcRequestContext> ContextStore { get; } = new ConcurrentDictionary<IInjector, IRpcRequestContext>(); // TODO: "concurrencyLevel" beallitasa

        /// <inheritdoc/>
        public ModuleInvocation ModuleInvocation { get; private set; } = ModuleInvocationBuilder.EmptyDelegate;

        /// <inheritdoc/>
        public override IRequestHandler Build(IRequestHandler next)
        {
            if (ModuleInvocation == ModuleInvocationBuilder.EmptyDelegate)
                lock (ModuleInvocationBuilder)
                    if (ModuleInvocation == ModuleInvocationBuilder.EmptyDelegate)
                        ModuleInvocation = ModuleInvocationBuilder.Build();

            return new ModuleInvocationHandler(next, this);
        }

        /// <summary>
        /// Creates a new <see cref="Modules"/> instance.
        /// </summary>
        public Modules(WebServiceBuilder webServiceBuilder) : base(webServiceBuilder) => WebServiceBuilder
            .ConfigureServices(svcs => svcs
                .Factory<IRpcRequestContext>(scope => ContextStore[scope] ?? throw new InvalidOperationException(), Lifetime.Scoped)
                .Service<IJsonSerializer, JsonSerializerBackend>(Lifetime.Singleton));

        /// <summary>
        /// Overrides the default serializer miplementation.
        /// </summary>
        public Modules ConfigureSerializer(Func<IInjector, IJsonSerializer> factory)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            WebServiceBuilder
                .ConfigureServices(svcs => svcs
                    .Remove<IJsonSerializer>()
                    .Factory<IJsonSerializer>(factory, Lifetime.Singleton));

            return this;
        }

        /// <summary>
        /// Registers a module to be accessible via Remote Procedure Call.
        /// </summary>
        public Modules Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface
        {
            ModuleInvocationBuilder.AddModule<TInterface>();
            WebServiceBuilder.ConfigureServices(svcs => svcs.Service<TInterface, TImplementation>(Lifetime.Scoped));
            return this;
        }

        /// <summary>
        /// Registers a module to be accessible via Remote Procedure Call.
        /// </summary>
        public Modules Register<TInterface>(Func<IInjector, TInterface> factory) where TInterface : class
        {
            ModuleInvocationBuilder.AddModule<TInterface>();
            WebServiceBuilder.ConfigureServices(svcs => svcs.Factory<TInterface>(factory, Lifetime.Scoped));
            return this;
        }
    }
}
