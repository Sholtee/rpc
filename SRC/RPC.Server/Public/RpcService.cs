/********************************************************************************
* RpcService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc
{
    using DI;
    using DI.Interfaces;
    
    using Internals;

    /// <summary>
    /// Implements the core RPC service functionality.
    /// </summary>
    public class RpcService : WebService, IModuleRegistry
    {
        private readonly ModuleInvocationBuilder FModuleInvocationBuilder;
        private ModuleInvocation? FModuleInvocation;

        #region Public
        /// <summary>
        /// The <see cref="IServiceContainer"/> associated with this service.
        /// </summary>
        public IServiceContainer Container { get; }

        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceContainer container, ModuleInvocationBuilder moduleInvocationBuilder) : base()
        {
            Container = container ?? throw new ArgumentNullException(nameof(container));
            FModuleInvocationBuilder = moduleInvocationBuilder ?? throw new ArgumentNullException(nameof(moduleInvocationBuilder));
        }

        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceContainer container) : this(container, new ModuleInvocationBuilder()) { }

        /// <summary>
        /// See <see cref="IModuleRegistry.Register{TInterface, TImplementation}"/>.
        /// </summary>
        public void Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface
        {
            if (IsStarted)
                throw new InvalidOperationException();

            Container.Service<TInterface, TImplementation>(Lifetime.Scoped);
            FModuleInvocationBuilder.AddModule<TInterface>();
        }

        /// <summary>
        /// See <see cref="IModuleRegistry.Register{TInterface}(Func{IInjector, TInterface})"/>.
        /// </summary>
        public void Register<TInterface>(Func<IInjector, TInterface> factory) where TInterface : class
        {
            if (IsStarted)
                throw new InvalidOperationException();

            Container.Factory(factory ?? throw new ArgumentNullException(nameof(factory)), Lifetime.Scoped);
            FModuleInvocationBuilder.AddModule<TInterface>();
        }

        /// <summary>
        /// See <see cref="WebService.Start(string)"/>.
        /// </summary>
        public override void Start(string url)
        {
            if (IsStarted)
                throw new InvalidOperationException();

            //
            // Elsonek hivjuk h ha megformed akkor a kiszolgalo el se induljon.
            //

            FModuleInvocation = FModuleInvocationBuilder.Build();

            base.Start(url);      
        }

        /// <summary>
        /// See <see cref="WebService.Stop"/>.
        /// </summary>
        public override void Stop()
        {
            if (!IsStarted)
                throw new InvalidOperationException();

            base.Stop();
        }
        #endregion

        #region Protected
        /// <inheritdoc/>
        protected override bool IsPreflight(HttpListenerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (base.IsPreflight(context)) 
            {
                context.Response.Headers["Access-Control-Allow-Methods"] = "POST";
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        protected override void PreCheck(HttpListenerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.HttpMethod.ToUpperInvariant() != "POST")
            {
                response.Headers[HttpResponseHeader.Allow] = "POST";

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
        }

        /// <summary>
        /// Processes HTTP requests asynchronously.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        protected override async Task Process(HttpListenerContext context) 
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            object? result;

            try
            {
                result = await InvokeModule(await RequestContext.Create(context.Request));
            }

            //
            // - A "catch" blokk ne az InvokeModule()-ban legyen h pl a RequestContext.Create() altal dobott
            //   hibakat is el tudjuk kapni
            // - A Http kiveteleket tovabb dobjuk h az os kezelje
            //

            catch (Exception ex) when (!(ex is HttpException))
            {
                result = ex;
            }

            await CreateResponse(result, context.Response);
        }

        /// <summary>
        /// Invokes a module method described by the <paramref name="context"/>.
        /// </summary>
        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        protected async virtual Task<object?> InvokeModule(IRequestContext context) 
        {
            if (context == null) 
                throw new ArgumentNullException(nameof(context));

            if (FModuleInvocation == null)
                throw new InvalidOperationException();

            await using IInjector injector = Container.CreateInjector();

            injector.UnderlyingContainer.Instance(context);

            return await FModuleInvocation(injector, context);
        }

        /// <summary>
        /// Sets the HTTP response.
        /// </summary>
        protected virtual async Task CreateResponse(object? result, HttpListenerResponse response)
        {
            if (response == null) 
                throw new ArgumentNullException(nameof(response));

            Stream? outputStream = null; // Ertekadas azert h ne reklamaljon a fordito
            try
            {
                switch (result)
                {
                    case Stream stream:
                        response.ContentType = "application/octet-stream";
                        response.ContentEncoding = null;
                        outputStream = stream;
                        break;
                    case Exception ex:
                        response.ContentType = "application/json";
                        response.ContentEncoding = Encoding.UTF8;
                        await JsonSerializer.SerializeAsync(outputStream = new MemoryStream(), new RpcResponse
                        {
                            Exception = new ExceptionInfo
                            {
                                TypeName = ex.GetType().AssemblyQualifiedName,
                                Message = ex.Message,
                                Data = ex.Data
                            }
                        });
                        break;
                    default:
                        response.ContentType = "application/json";
                        response.ContentEncoding = Encoding.UTF8;
                        await JsonSerializer.SerializeAsync(outputStream = new MemoryStream(), new RpcResponse
                        {
                            Result = result
                        });
                        break;
                }

                response.StatusCode = (int) HttpStatusCode.OK;

                //
                // A "response.ContentLength64 = response.OutputStream.Length" nem mukodne
                //

                response.ContentLength64 = outputStream.Length;
                outputStream.Seek(0, SeekOrigin.Begin);
                await outputStream.CopyToAsync(response.OutputStream);
            }

            //
            // Mindenkepp felszabaditjuk a Stream-et meg ha RPC interface-metodus adta vissza akkor is
            //

            finally { outputStream?.Dispose(); }

            response.Close();
        }
        #endregion
    }
}
