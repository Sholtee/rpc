/********************************************************************************
* RequestTimeoutHandler.cs                                                      *
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
    using Internals;

    /// <summary>
    /// Specifies the <see cref="RequestTimeoutHandler"/> configuration.
    /// </summary>
    public interface IRequestTimeoutHandlerConfig
    {
        /// <summary>
        /// The request timeout.
        /// </summary>
        public TimeSpan Timeout { get; }
    }

    /// <summary>
    /// Adds a timeout to the request processing.
    /// </summary>
    public class RequestTimeoutHandler : RequestHandlerBase<IRequestTimeoutHandlerConfig>
    {
        /// <summary>
        /// Creates a new <see cref="RequestTimeoutHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public RequestTimeoutHandler(IRequestHandler next, IRequestTimeoutHandlerConfig config): base(next, config) { }

        /// <inheritdoc/>
        public override async Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            //
            // A feldolgozonak ket esetben kell leallnia:
            //   1) Adott idointervallumon belul nem sikerult a feladatat elvegeznie
            //   2) Maga a WebService kerul leallitasra
            //

            using CancellationTokenSource taskCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            Task task = Next.HandleAsync(scope, context, taskCancellation.Token);
            if (!await task.WaitAsync(Config.Timeout))
                //
                // Elkuldjuk a megszakitas kerelmet a feldolgozonak.
                //

                taskCancellation.Cancel();

            //
            // Meg ha tudjuk is hogy a "task" befejezodott akkor is legyen "await" hogy ha kivetel
            // volt akkor azt tovabb tudjuk dobni.
            //

            await task;
        }
    }

    /// <summary>
    /// Configures the amount of time allowed to serve a request.
    /// </summary>
    public class RequestTimeout : RequestHandlerBuilder, IRequestTimeoutHandlerConfig
    {
        /// <summary>
        /// Creates a new <see cref="RequestTimeout"/> instance.
        /// </summary>
        public RequestTimeout(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent) : base(webServiceBuilder, parent) { }

        /// <summary>
        /// The request timeout.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <inheritdoc/>
        public override IRequestHandler Build(IRequestHandler next) => new RequestTimeoutHandler(next, this);
    }
}
