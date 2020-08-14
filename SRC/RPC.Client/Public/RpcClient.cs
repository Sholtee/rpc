/********************************************************************************
* RpcClient.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc
{
    using Primitives.Patterns;
    
    /// <summary>
    /// Invokes a remote module created by RPC.NET.
    /// </summary>
    [Obsolete("This type is obsolete, use RpcClientFactory instead!")]
    public sealed class RpcClient<TInterface>: Disposable where TInterface: class
    {
        private readonly RpcClientFactory FFactory;

        /// <inheritdoc/>
        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged) FFactory.Dispose();

            base.Dispose(disposeManaged);
        }

        #region Public
        /// <summary>
        /// The (optional) session ID related to this instance.
        /// </summary>
        public string? SessionId
        {
            get => FFactory.SessionId;
            set => FFactory.SessionId = value;
        }

        /// <summary>
        /// The address of the remote host (e.g.: "www.mysite.com:1986/api").
        /// </summary>
        public string Host => FFactory.Host;

        /// <summary>
        /// Represents the request timeout.
        /// </summary>
        public TimeSpan Timeout 
        {
            get => FFactory.Timeout;
            set => FFactory.Timeout = value;
        }

        /// <summary>
        /// Headers sent along with each request.
        /// </summary>
        /// <remarks>You should not set "content-type", it is done by te system automatically.</remarks>
        public IDictionary<string, string> CustomHeaders => FFactory.CustomHeaders;

        /// <summary>
        /// Creates a new <see cref="RpcClient{TInterface}"/> instance.
        /// </summary>
        public RpcClient(string host) => FFactory = new RpcClientFactory(host);

        /// <summary>
        /// The generated proxy instance related to this client.
        /// </summary>
        public TInterface Proxy => FFactory.CreateClient<TInterface>();
        #endregion
    }
}
