/********************************************************************************
* SessionProviderHandler.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Properties;

    /// <summary>
    /// 
    /// </summary>
    public interface ISessionProviderConfig
    {
    }

    /// <summary>
    /// Contains the session information if available
    /// </summary>
    file interface ISessionHolder
    {
        /// <summary>
        /// 
        /// </summary>
        IHttpSession? Value { get; set; }
    }

    /// <summary>
    /// Makes the actual session accessible via DI.
    /// </summary>
    public class SessionProviderHandler : RequestHandlerBase<ISessionProviderConfig>
    {
        /// <summary>
        /// Creates a new <see cref="SessionProviderHandler"/> instance.
        /// </summary>
        public SessionProviderHandler(IRequestHandler next, ISessionProviderConfig config) : base(next, config)
        {
        }

        /// <inheritdoc/>
        public override async Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            ISessionHolder sessionHolder = scope.Get<ISessionHolder>();
            sessionHolder.Value = context;
            await Next.HandleAsync(scope, context, cancellation);
        }
    }

    /// <summary>
    /// Makes the actual session accessible via DI.
    /// </summary>
    public class SessionProvider : RequestHandlerBuilder, ISessionProviderConfig
    {
        private sealed class SessionHolder : ISessionHolder
        {
            public IHttpSession? Value { get; set; }
        }

        private static IHttpSession GetSession(IInjector injector) =>
            injector.Get<ISessionHolder>(null).Value ?? throw new InvalidOperationException(Errors.NO_SESSION);

        /// <summary>
        /// Creates a new <see cref="SessionProvider"/> instance.
        /// </summary>
        public SessionProvider(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent) : base(webServiceBuilder, parent) { }

        /// <inheritdoc/>
        public override IRequestHandler Build(IRequestHandler next)
        {
            ServiceOptions suppressDispose = ServiceOptions.Default with { DisposalMode = ServiceDisposalMode.Suppress };

            WebServiceBuilder.ConfigureServices
            (
                (svcs, lifetimes) => svcs
                    .Service<ISessionHolder, SessionHolder>(lifetimes.Scoped(), suppressDispose)
                    .Factory<IHttpRequest>(static i => GetSession(i).Request, lifetimes.Scoped(), suppressDispose)
                    .Factory<IHttpResponse>(static i => GetSession(i).Response, lifetimes.Scoped(), suppressDispose)
            );

            return new SessionProviderHandler(next, this);
        }
    }
}
