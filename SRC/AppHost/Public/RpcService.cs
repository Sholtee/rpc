/********************************************************************************
* RpcService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Solti.Utils.AppHost
{
    using DI;
    using DI.Interfaces;
    
    using Internals;
    using Primitives;

    /// <summary>
    /// RPC service
    /// </summary>
    public class RpcService : WebService, IModuleRegistry
    {
        private readonly IServiceContainer FContainer;

        private readonly ModuleInvocationBuilder FModuleInvocationBuilder;

        private ModuleInvocation? FModuleInvocation;

        #region Public
        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceContainer container, ModuleInvocationBuilder moduleInvocationBuilder) : base()
        {
            FContainer = container ?? throw new ArgumentNullException(nameof(container));
            FModuleInvocationBuilder = moduleInvocationBuilder ?? throw new ArgumentNullException(nameof(moduleInvocationBuilder));
        }

        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        public RpcService(IServiceContainer container) : this(container, new ModuleInvocationBuilder()) { }

        /// <summary>
        /// See <see cref="IModuleRegistry.Register{TInterface, TImplementation}"/>.
        /// </summary>
        public virtual void Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface
        {
            if (IsStarted)
                throw new InvalidOperationException();

            FContainer.Service<TInterface, TImplementation>(Lifetime.Scoped);
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
        /// <summary>
        /// Processes HTTP requests asynchronously.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        protected override async Task ProcessRequestContext(HttpListenerContext context) 
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            object? result;

            try
            {
                result = await InvokeModule(await RequestContext.Create(context.Request));
            }
            
            //
            // A "catch" blokk ne az InvokeModule()-ban legyen h pl a RequestContext.Create() altal dobott
            // hibakat is el tudjuk kapni
            //

            catch (Exception ex)
            {
                result = ex;
            }

            await CreateResponse(result, context.Response);
        }

        /// <summary>
        /// Invokes a module method described by the <paramref name="context"/>.
        /// </summary>
        protected async virtual Task<object?> InvokeModule(IRequestContext context) 
        {
            if (context == null) 
                throw new ArgumentNullException(nameof(context));

            if (FModuleInvocation == null)
                throw new InvalidOperationException();

            await using IInjector injector = FContainer.CreateInjector();

            injector.UnderlyingContainer.Instance(context);

            object? result = FModuleInvocation(injector, context);

            //
            // Ha a modul metodusnak Task a visszaterese akkor meg meg kell varni az eredmenyt (es addig
            // az injector sem szabadithato fel).
            //

            if (result is Task task)
            {
                await task;

                Type taskType = task.GetType();

                result = !taskType.IsGenericType
                    ? null
                    : taskType
                        .GetProperty(nameof(Task<object>.Result))
                        .ToGetter()
                        .Invoke(task);
            }

            return result;
        }

        /// <summary>
        /// Sets the HTTP response.
        /// </summary>
        protected virtual async Task CreateResponse(object? result, HttpListenerResponse response)
        {
            if (response == null) 
                throw new ArgumentNullException(nameof(response));

            switch(result)
            {
                case Stream stream:
                    response.StatusCode = (int) HttpStatusCode.OK;
                    response.ContentType = "application/octet-stream";
                    response.ContentEncoding = null;
                    stream.CopyTo(response.OutputStream);
                    break;
                case Exception ex:
                    response.StatusCode = (int) GetErrorCode(ex);
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await JsonSerializer.SerializeAsync(response.OutputStream, new RpcResponse 
                    {
                        Exception = new ExceptionInfo 
                        {
                            TypeName = ex.GetType().FullName,
                            Instance = ex
                        }
                    });
                    break;
                default:
                    response.StatusCode = (int) HttpStatusCode.OK;
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await JsonSerializer.SerializeAsync(response.OutputStream, new RpcResponse 
                    {
                        Result = result
                    });
                    break;
            }

            response.ContentLength64 = response.OutputStream.Length;
            response.Close();
        }

        /// <summary>
        /// Gets the HTTP status code associated with the given exception.
        /// </summary>
        protected virtual HttpStatusCode GetErrorCode(Exception ex) 
        {
            if (ex is InvalidCredentialException) return HttpStatusCode.Forbidden;
            if (ex is UnauthorizedAccessException) return HttpStatusCode.Unauthorized;

            return HttpStatusCode.InternalServerError;
        }
        #endregion
    }
}
