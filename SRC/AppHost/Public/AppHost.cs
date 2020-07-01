/********************************************************************************
* AppHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
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
