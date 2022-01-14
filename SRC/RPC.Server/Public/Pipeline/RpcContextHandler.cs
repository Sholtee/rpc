/********************************************************************************
* RpcContextHandler.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;

    /// <summary>
    /// Adds the <see cref="IRpcRequestContext"/> instance to the session.
    /// </summary>
    public class RpcContextHandler : IRequestHandler
    {
        [ThreadStatic]
        internal static IRpcRequestContext? CurrentContext;

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// Creates a new <see cref="RpcContextHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public RpcContextHandler(IRequestHandler next) => Next = next ?? throw new ArgumentNullException(nameof(next));

        /// <inheritdoc/>
        public async Task Handle(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            HttpListenerRequest request = context.Request;

            //
            // RpcRequestContext konstruktora hiba eseten HttpException-t dob.
            //

            CurrentContext = new RpcRequestContext(request, context.Cancellation);
            try
            {
                await Next.Handle(context);
            } finally
            {
                CurrentContext = null;
            }
        }
    }

    /// <summary>
    /// Adds the <see cref="IRpcRequestContext"/> instance to the session.
    /// </summary>
    public class RpcContext : RequestHandlerFactory
    {
        /// <inheritdoc/>
        public override IRequestHandler Create(IRequestHandler next) => new RpcContextHandler(next);

        /// <inheritdoc/>
        public override void AddTo(IServiceCollection services)
        {
            services.Factory<IRpcRequestContext>(_ => RpcContextHandler.CurrentContext ?? throw new InvalidOperationException(), Lifetime.Scoped);

            base.AddTo(services);
        }
    }
}
