/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Hosting
{
    using DI.Interfaces;
    using Interfaces;
    using Rpc.Internals;

    /// <summary>
    /// Represents the base class of the host application.
    /// </summary>
    public abstract partial class AppHostBase: CommandLineApplication
    {
        /// <summary>
        /// Creates a new <see cref="AppHostBase"/> instance.
        /// </summary>
        protected AppHostBase(IReadOnlyList<string> args) : base(args) { }

        /// <summary>
        /// Creates a new <see cref="WebServiceBuilder"/> instance.
        /// </summary>
        /// <remarks>Override this method if you want to use your own builder.</remarks>
        protected virtual WebServiceBuilder CreateServiceBuilder() => new(DiProvider);

        /// <summary>
        /// Returns the underlying <see cref="Rpc.WebService"/>.
        /// </summary>
        /// <remarks>This property is set only once, after successful service configuration.</remarks>
        public WebService? WebService { get; private set; }

        /// <summary>
        /// The DI backend.
        /// </summary>
        /// <remarks>This property can be set in initialization time only.</remarks>
        public IDiProvider DiProvider { get; init; } = new DiProvider();

        /// <summary>
        /// Invoked once on configuration phase.
        /// </summary>
        /// <remarks>Override this method to configure the RPC service being built.</remarks>
        public abstract void OnConfigure(WebServiceBuilder serviceBuilder);

        /// <summary>
        /// Invoked once on configuration phase.
        /// </summary>
        /// <remarks>The primary place to configure services is the <see cref="OnConfigure(WebServiceBuilder)"/> method (using the <see cref="WebServiceBuilder.ConfigureServices(Action{IServiceCollection})"/>). Override this method only if you need scoped installation logic (<see cref="OnInstall(IInjector)"/> or <see cref="OnUnInstall(IInjector)"/>).</remarks>
        public virtual void OnConfigureServices(IServiceCollection services) { }

        /// <summary>
        /// Called once when the <see cref="WebService"/> is built successfully.
        /// </summary>
        /// <remarks>Override this method if you need to implement one-time initialization logic that depends on the built service.</remarks>
        public virtual void OnBuilt() {}

        /// <summary>
        /// Called on host start
        /// </summary>
        /// <remarks>In most of cases you should not override this method. To configure your service instance use the <see cref="OnConfigure(WebServiceBuilder)"/> and <see cref="OnBuilt"/> methods.</remarks>
        public virtual void OnStart()
        {
            if (WebService is null)
            {
                WebServiceBuilder serviceBuilder = CreateServiceBuilder();

                OnConfigure(serviceBuilder);
                OnConfigureServices(DiProvider.Services);

                WebService = serviceBuilder.Build();
                OnBuilt();
            }

            WebService
                .Start()
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Called on host termination
        /// </summary>
        /// <remarks>Override this method if you need to do some resource cleanup.</remarks>
        public virtual void OnStop() => WebService?
            .Stop()
            .GetAwaiter()
            .GetResult();
    }
}
