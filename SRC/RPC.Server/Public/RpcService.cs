/********************************************************************************
* RpcService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc
{
    using DI;
    using DI.Interfaces;

    using Interfaces;
    using Internals;
    using Properties;

    /// <summary>
    /// Implements the core RPC service functionality.
    /// </summary>
    public class RpcService : WebService, IModuleRegistry
    {
        private const string META_REQUEST = nameof(META_REQUEST);

        private readonly ModuleInvocationBuilder FModuleInvocationBuilder;
        private readonly JsonSerializerOptions FSerializerOptions;
        private ModuleInvocation? FModuleInvocation;
        private IScopeFactory? FScopeFactory;

        private sealed class MetaReaderServiceEntry : AbstractServiceEntry
        {
            private object? FInstance;

            private MetaReaderServiceEntry(MetaReaderServiceEntry original, IServiceRegistry owner) : base(original.Interface, original.Name, null, owner)
            {
                MetaName = original.MetaName;
                State = ServiceEntryStates.Built;
            }

            public MetaReaderServiceEntry(Type @interface, string metaName) : base(@interface, null, null, null) => MetaName = metaName;

            public string MetaName { get; }

            public override object CreateInstance(IInjector scope) => throw new InvalidOperationException();

            public override object GetSingleInstance() => FInstance ??= (Owner as IInjector ?? throw new InvalidOperationException()).Meta(MetaName)!;

            public override AbstractServiceEntry CopyTo(IServiceRegistry owner) => new MetaReaderServiceEntry(this, owner);
        }

        #region Public
        /// <summary>
        /// The <see cref="IServiceCollection"/> associated with this service.
        /// </summary>
        public IServiceCollection ServiceCollection { get; }

        /// <summary>
        /// Controls the <see cref="JsonSerializer"/> related to this RPC service.
        /// </summary>
        /// <remarks>Don't change serialization options after the first module was registered. These options will be applied to serialization and deserialization as well.</remarks>
        public JsonSerializerOptions SerializerOptions 
        {
            //
            // A modul metodusokhoz tartozo kontextus tartalmazza a beallitasok masolatat -> modul regisztralas
            // utan mar nem jo otlet modositani.
            //

            get => !FModuleInvocationBuilder.Modules.Any()
                ? FSerializerOptions
                : throw new InvalidOperationException(); // TODO: message
        }

        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceCollection serviceCollection, JsonSerializerOptions serializerOptions, ModuleInvocationBuilder moduleInvocationBuilder) : base()
        {
            ServiceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));
            FModuleInvocationBuilder = moduleInvocationBuilder ?? throw new ArgumentNullException(nameof(moduleInvocationBuilder));
            FSerializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
            LoggerFactory = () => TraceLogger.Create<RpcService>();

            ServiceCollection.Register(new MetaReaderServiceEntry(typeof(IRequestContext), META_REQUEST));
        }

        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceCollection serviceCollection, ModuleInvocationBuilder moduleInvocationBuilder) : this(serviceCollection, new JsonSerializerOptions(), moduleInvocationBuilder) {}

        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceCollection serviceCollection, JsonSerializerOptions serializerOptions) : this(serviceCollection, serializerOptions, new ModuleInvocationBuilder(serializerOptions)) {}

        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceCollection serviceCollection) : this(serviceCollection, new JsonSerializerOptions()) { }

        /// <summary>
        /// See <see cref="IModuleRegistry.Register{TInterface, TImplementation}"/>.
        /// </summary>
        public void Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface
        {
            if (IsStarted)
                throw new InvalidOperationException();

            ServiceCollection.Service<TInterface, TImplementation>(Lifetime.Scoped);
            FModuleInvocationBuilder.AddModule<TInterface>();
        }

        /// <summary>
        /// See <see cref="IModuleRegistry.Register{TInterface}(Func{IInjector, TInterface})"/>.
        /// </summary>
        public void Register<TInterface>(Func<IInjector, TInterface> factory) where TInterface : class
        {
            if (IsStarted)
                throw new InvalidOperationException();

            ServiceCollection.Factory(factory ?? throw new ArgumentNullException(nameof(factory)), Lifetime.Scoped);
            FModuleInvocationBuilder.AddModule<TInterface>();
        }

        /// <inheritdoc/>
        public override void Start(string url)
        {
            if (IsStarted)
                throw new InvalidOperationException();

            ILogger? logger = LoggerFactory?.Invoke();
            logger?.LogInformation(Trace.STARTING_RPC_SERVICE);

            try
            {
                //
                // Elsonek hivjuk h ha megformed akkor a kiszolgalo el se induljon.
                //

                FModuleInvocation = FModuleInvocationBuilder.Build();
                FScopeFactory = ScopeFactory.Create(ServiceCollection);

                base.Start(url);
            }
            catch (Exception ex) 
            {
                logger?.LogError(ex, Trace.STARTING_RPC_SERVICE_FAILED);
                throw;
            }
        }

        /// <inheritdoc/>
        public override void Stop()
        {
            if (!IsStarted)
                throw new InvalidOperationException();

            base.Stop();
        }
        #endregion

        #region Protected
        /// <inheritdoc/>
        protected override void Dispose(bool disposeManaged)
        {
            base.Dispose(disposeManaged); // leallitja a kiszolgalot
            FScopeFactory?.Dispose();
        }

        /// <inheritdoc/>
        protected override void SetAcHeaders(HttpListenerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            base.SetAcHeaders(context);

            context.Response.Headers["Access-Control-Allow-Methods"] = "POST";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Content-Length";
        }

        /// <summary>
        /// Processes HTTP requests asynchronously.
        /// </summary>
        #pragma warning disable CS3001 // ILogger is not CLS-compliant
        protected override async Task Process(HttpListenerContext context, ILogger? logger, CancellationToken cancellation)
        #pragma warning restore CS3001
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            cancellation.ThrowIfCancellationRequested();

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            //
            // Eloellenorzesek HTTP hibat generalnak -> NE a try-catch blokkban legyenek
            //

            if (request.HttpMethod.ToUpperInvariant() != "POST")
            {
                throw new HttpException
                {
                    Status = HttpStatusCode.MethodNotAllowed
                };
            }

            //
            // Content-Type lehet NULL a kodolas viszont nem
            //

            if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true || request.ContentEncoding.WebName != "utf-8")
            {
                throw new HttpException
                {
                    Status = HttpStatusCode.BadRequest
                };
            }

            object? result;

            try
            {
                result = await InvokeModule(new RequestContext(request, cancellation), logger);
            }

            catch (OperationCanceledException ex) // ez megeszi a TaskCanceledException-t is
            {
                //
                // Mivel itt mar "cancellation.IsCancellationRequested" jo esellyel igaz ezert h a CreateResponse() ne 
                // dobjon egybol TaskCanceledException-t (ami HTTP 500-at generalna) ne az eredeti "cancellation"-t 
                // adjuk at.
                //

                cancellation = default;
                result = ex;
            }

            catch (HttpException)
            {
                //
                // Egyedi HTTP hibakod is megadhato, ekkor nem szerializalunk.
                //

                throw;
            }

            #pragma warning disable CA1031 // We have to catch all kind of exceptions here
            catch (Exception ex)
            #pragma warning restore CA1031
            {
                //
                // Kulomben valid valasz fogja tartalmazni a hibat.
                //

                result = ex;
            }

            await CreateResponse(result, response, cancellation);
        }

        /// <summary>
        /// Invokes a module method described by the <paramref name="context"/>.
        /// </summary>
        #pragma warning disable CS3001 // ILogger is not CLS-compliant
        protected async virtual Task<object?> InvokeModule(IRequestContext context, ILogger? logger)
        #pragma warning restore CS3001 // Argument type is not CLS-compliant
        {
            if (context is null) 
                throw new ArgumentNullException(nameof(context));

            if (FModuleInvocation is null)
                throw new InvalidOperationException();

            await using IInjector injector = FScopeFactory!.CreateScope();

            //
            // A kontextust elerhetik a modulok fuggosegkent.
            //

            injector.Meta(META_REQUEST, context);

            //
            // Naplozzuk a metodus hivast.
            //

            using IDisposable? scope = logger?.BeginScope(new Dictionary<string, object>
            {
                [nameof(context.Module)]    = context.Module,
                [nameof(context.Method)]    = context.Method,
                [nameof(context.SessionId)] = context.SessionId ?? "NULL"
            });

            logger?.LogInformation(Trace.BEGINNING_INVOCATION);
            var stopWatch = Stopwatch.StartNew();

            try
            {
                object? result = await FModuleInvocation(injector, context);

                logger?.LogInformation(string.Format(Trace.Culture, Trace.INVOCATION_SUCCESSFUL, stopWatch.ElapsedMilliseconds));
                stopWatch.Stop();

                return result;
            }
            catch (Exception ex) 
            {
                logger?.LogError(ex, Trace.INVOCATION_FAILED);
                throw;
            }
        }

        /// <summary>
        /// Sets the HTTP response.
        /// </summary>
        protected virtual async Task CreateResponse(object? result, HttpListenerResponse response, CancellationToken cancellation)
        {
            if (response is null) 
                throw new ArgumentNullException(nameof(response));

            switch (result)
            {
                case Stream stream:
                    response.ContentType = "application/octet-stream";
                    response.ContentEncoding = null;
                    if (stream.CanSeek)
                        stream.Seek(0, SeekOrigin.Begin);
                    await stream.CopyToAsync
                    (
                        response.OutputStream
#if NETSTANDARD2_1_OR_GREATER
                        , cancellation
#endif
                    );      
                    stream.Dispose();
                    break;
                case Exception ex:
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await JsonSerializer.SerializeAsync(response.OutputStream, new RpcResponse
                    {
                        Exception = new ExceptionInfo
                        {
                            TypeName = ex.GetType().AssemblyQualifiedName,
                            Message = ex.Message,
                            Data = ex.Data
                        }
                    }, FSerializerOptions, cancellation);
                    break;
                default:
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await JsonSerializer.SerializeAsync(response.OutputStream, new RpcResponse
                    {
                        Result = result
                    }, FSerializerOptions, cancellation);
                    break;
            }

            //
            // Statuszt es hosszt nem kell beallitani.
            //

            response.Close();
        }
        #endregion
    }
}
