/********************************************************************************
* ModuleInvocationHandler.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Properties;

    /// <summary>
    /// Handles RPC module invocatios.
    /// </summary>
    public class ModuleInvocationHandler : IRequestHandler
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
        protected virtual async Task CreateResponse(object? result, IHttpResponse response)
        {
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
                    await SafeSerializer.SerializeAsync(response.Payload, new RpcResponse
                    {
                        Exception = new ExceptionInfo
                        {
                            TypeName = ex.GetType().AssemblyQualifiedName,
                            Message  = ex.Message,
                            Data     = ex.Data
                        }
                    }, Parent.SerializerOptions);
                    break;
                default:
                    response.Headers["Content-Type"] = "application/json";
                    await SafeSerializer.SerializeAsync(response.Payload, new RpcResponse { Result = result }, Parent.SerializerOptions);
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
                paramz[nameof(RpcRequestContext.SessionId)],
                paramz[nameof(RpcRequestContext.Module)] ?? throw new HttpException(Errors.NO_MODULE) { Status = HttpStatusCode.BadRequest },
                paramz[nameof(RpcRequestContext.Method)] ?? throw new HttpException(Errors.NO_METHOD) { Status = HttpStatusCode.BadRequest },
                request,
                cancellation           
            );
        }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// The parent instance.
        /// </summary>
        public Modules Parent { get; }

        /// <summary>
        /// Creates a new <see cref="ModuleInvocationHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public ModuleInvocationHandler(IRequestHandler next, Modules parent)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Next   = next   ?? throw new ArgumentNullException(nameof(next));
        }

        //
        // A handler-ben nem kell semmit sem naplozni mert az itt mar oldhato aspektusokkal.
        //

        /// <inheritdoc/>
        public async Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            IRpcRequestContext rpcRequestContext = Parent.ContextStore[scope] = CreateContext(context, cancellation);
            object? result;

            try
            {
                result = await Parent.ModuleInvocation!(scope, rpcRequestContext, Parent.SerializerOptions);
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
                Parent.ContextStore.Remove(scope);
            }

            //
            // A valasz kiirasat mar nem lehet megszakitani.
            //

            await CreateResponse(result, context.Response);
            await Next.HandleAsync(scope, context, cancellation);
        }
    }

    /// <summary>
    /// Configures services to be accessible via Remote Procedure Call.
    /// </summary>
    public class Modules : RequestHandlerBuilder, IModuleRegistry
    {
        private ModuleInvocationBuilder ModuleInvocationBuilder { get; } = new();

        //
        // Az egyes scope-okhoz tartozo kontextust nem lehet ThreadLocal-ban tarolni:
        //
        //   threadLocal = context;
        //   await someAsyncTask();
        //   threadLocal == null;
        //

        internal IDictionary<IInjector, IRpcRequestContext> ContextStore { get; } = new ConcurrentDictionary<IInjector, IRpcRequestContext>(); // TODO: "concurrencyLevel" beallitasa

        internal ModuleInvocation? ModuleInvocation { get; private set; }

        /// <inheritdoc/>
        public override IRequestHandler Build(IRequestHandler next)
        {
            if (ModuleInvocation is null)
                lock (ModuleInvocationBuilder)
                    if (ModuleInvocation is null)
                        ModuleInvocation = ModuleInvocationBuilder.Build();

            return new ModuleInvocationHandler(next, this);
        }

        /// <summary>
        /// Creates a new <see cref="Modules"/> instance.
        /// </summary>
        public Modules(WebServiceBuilder webServiceBuilder) : base(webServiceBuilder) =>
            WebServiceBuilder.ConfigureServices(svcs => svcs.Factory<IRpcRequestContext>(scope => ContextStore[scope] ?? throw new InvalidOperationException(), Lifetime.Scoped));

        /// <summary>
        /// The serializer options.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions();

        /// <inheritdoc/>
        public IModuleRegistry Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface
        {
            ModuleInvocationBuilder.AddModule<TInterface>();
            WebServiceBuilder.ConfigureServices(svcs => svcs.Service<TInterface, TImplementation>(Lifetime.Scoped));
            return this;
        }

        /// <inheritdoc/>
        public IModuleRegistry Register<TInterface>(Func<IInjector, TInterface> factory) where TInterface : class
        {
            ModuleInvocationBuilder.AddModule<TInterface>();
            WebServiceBuilder.ConfigureServices(svcs => svcs.Factory<TInterface>(factory, Lifetime.Scoped));
            return this;
        }
    }
}
