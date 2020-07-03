/********************************************************************************
* AppHost.cs                                                                    *
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
    using Primitives.Patterns; 

    /// <summary>
    /// AppHost
    /// </summary>
    public class AppHostBase: Disposable
    {
        /// <summary>
        /// The root service container.
        /// </summary>
        public IServiceContainer RootContainer { get; } = new ServiceContainer();

        private readonly ModuleInvocationBuilder FModuleInvocationBuilder = new ModuleInvocationBuilder();

        private ModuleInvocation? FModuleInvocation;

        /// <summary>
        /// Processes HTTP requests asynchronously.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        protected virtual async Task<object?> ProcessRequest(HttpListenerRequest request) 
        {
            try
            {
                IRequestContext context = await RequestContext.Create(request ?? throw new ArgumentNullException(nameof(request)));

                object? result = InvokeModule(context);

                //
                // Ha a modul metodusnak Task a visszaterese akkor meg meg kell varni az eredmenyt.
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
            
            //
            // A "catch" blokk ne az InvokeModule()-ban legyen h pl a RequestContext.Create() altal dobott
            // hibakat is el tudjuk kapni
            //

            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Invokes a module method described by the <paramref name="context"/>.
        /// </summary>
        protected virtual object? InvokeModule(IRequestContext context) 
        {
            using (IInjector injector = CreateInjector())
            {
                injector.UnderlyingContainer.Instance(context);

                return FModuleInvocation!(injector, context);
            }
        }

        /// <summary>
        /// Creates a new <see cref="IInjector"/> instance for a session.
        /// </summary>
        /// <remarks>You should override this method if you're using child containers.</remarks>
        protected virtual IInjector CreateInjector() => RootContainer.CreateInjector();

        /// <summary>
        /// Sets the HTTP response.
        /// </summary>
        protected virtual async Task CreateResponse(object? result, HttpListenerResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

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
                    await JsonSerializer.SerializeAsync(response.OutputStream, new object?[2] 
                    { 
                        null,  
                        new
                        {
                            ExceptionType = ex.GetType().FullName,
                            Exception = ex
                        }
                    });
                    break;
                default:
                    response.StatusCode = (int) HttpStatusCode.OK;
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await JsonSerializer.SerializeAsync(response.OutputStream, new object?[2]
                    {
                        result,
                        null
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
            if (ex is InvalidCredentialException) return  HttpStatusCode.Unauthorized;
            if (ex is UnauthorizedAccessException) return HttpStatusCode.Forbidden;
            
            return HttpStatusCode.InternalServerError;
        }

        /// <summary>
        /// Build the <see cref="AppHost"/>
        /// </summary>
        public void Build() 
        {
            if (FModuleInvocation != null)
                throw new InvalidOperationException(); // TODO

            FModuleInvocation = FModuleInvocationBuilder.Build();
        }
    }
}
