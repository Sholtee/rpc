/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Hosting
{
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
        protected virtual WebServiceBuilder CreateServiceBuilder() => new();

        /// <summary>
        /// Returns the underlying <see cref="Rpc.WebService"/>.
        /// </summary>
        /// <remarks>This property is set only once, after successful service configuration.</remarks>
        public WebService? WebService { get; private set; }

        /// <summary>
        /// Invoked once on service setup.
        /// </summary>
        /// <remarks>Override this method to configure the RPC service being built.</remarks>
        public abstract void OnConfigure(WebServiceBuilder serviceBuilder);

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
                WebService = serviceBuilder.Build();
                OnBuilt();
            }

            WebService.Start();
        }

        /// <summary>
        /// Called on host termination
        /// </summary>
        /// <remarks>Override this method if you need to do some resource cleanup.</remarks>
        public virtual void OnStop() => WebService?.Stop();
    }
}
