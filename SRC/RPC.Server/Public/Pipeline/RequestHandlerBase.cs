/********************************************************************************
* RequestHandlerBuilder.cs                                                      *
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

    /// <summary>
    /// Defines the base class for request handlers.
    /// </summary>
    public abstract class RequestHandlerBase<TConfiguration> : IRequestHandler where TConfiguration : class
    {
        /// <summary>
        /// The configuration.
        /// </summary>
        public TConfiguration Config { get; }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// Creates a new <see cref="RequestHandlerBase{TConfiguration}"/> instance.
        /// </summary>
        protected RequestHandlerBase(IRequestHandler next, TConfiguration config)
        {
            Next = next ?? throw new ArgumentNullException(nameof(next));
            Config = config ?? throw new ArgumentNullException(nameof(next));
        }

        /// <inheritdoc/>
        public abstract Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation);
    }
}
