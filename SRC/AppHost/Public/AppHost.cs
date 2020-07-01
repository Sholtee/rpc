/********************************************************************************
* AppHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Security.Authentication;
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
        private readonly IServiceContainer FRootContainer = new ServiceContainer();

        private readonly ModuleInvocationBuilder FModuleInvocationBuilder = new ModuleInvocationBuilder();

        private ModuleInvocation? FModuleInvocation;

        /// <summary>
        /// Processes HTTP requests asynchronously.
        /// </summary>
        protected virtual async Task<object?> ProcessRequest(HttpListenerRequest request) 
        {
            IRequestContext context = await RequestContext.Create(request ?? throw new ArgumentNullException(nameof(request)));

            using (IInjector injector = FRootContainer.CreateInjector()) 
            {
                injector.UnderlyingContainer.Instance(context);

                object? result = FModuleInvocation!(injector, context);

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
        }

        /// <summary>
        /// Sets the HTTP response.
        /// </summary>
        protected virtual async Task CreateResponse(object result, HttpListenerResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            response.AddHeader("Content-Type", "application/json");

            object?[] toBeSerialized = new object[2];

            if (result is Exception ex)
            {
                response.StatusCode = (int) GetErrorCode(ex);
                toBeSerialized[1] = ex;
            }
            else 
            {
                response.StatusCode = (int) HttpStatusCode.OK;
                toBeSerialized[0] = result;
            }

            await JsonSerializer.SerializeAsync(response.OutputStream, toBeSerialized);

            response.Close();
        }

        /// <summary>
        /// Gets the HTTP status code associated with the given exception.
        /// </summary>
        protected virtual HttpStatusCode GetErrorCode(Exception ex) 
        {
            if (ex is InvalidCredentialException) return  HttpStatusCode.Unauthorized;
            if (ex is UnauthorizedAccessException) return HttpStatusCode.Forbidden;
            
            return HttpStatusCode.BadRequest;
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
