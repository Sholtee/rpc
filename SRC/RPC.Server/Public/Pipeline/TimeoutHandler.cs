/********************************************************************************
* TimeoutHandler.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using Interfaces;

    /// <summary>
    /// Adds a timeout to the request processing.
    /// </summary>
    public class TimeoutHandler : IRequestHandler
    {
        /// <summary>
        /// The request timeout.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// Creates a new <see cref="TimeoutHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public TimeoutHandler(IRequestHandler next, in TimeSpan timeout)
        {
            Next = next ?? throw new ArgumentNullException(nameof(next));
            Timeout = timeout;
        }

        /// <inheritdoc/>
        public async Task HandleAsync(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            //
            // A feldolgozonak ket esetben kell leallnia:
            //   1) Adott idointervallumon belul nem sikerult a feladatat elvegeznie
            //   2) Maga a WebService kerul leallitasra
            //

            using CancellationTokenSource
                taskCancellation = new(),
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(taskCancellation.Token, context.Cancellation);

            Task task = Next.HandleAsync(new RequestContext(context) 
            { 
                Cancellation = linkedCancellation.Token
            });

            if (await Task.WhenAny(task, Task.Delay(Timeout)) != task)
                //
                // Elkuldjuk a megszakitas kerelmet a feldolgozonak.
                //

                taskCancellation.Cancel();

            //
            // Itt a kovetkezo esetek lehetnek:
            //   1) A feldolgozo idoben befejezte a feladatat, az "await" mar nem fog varakozni, jok vagyunk
            //   2) A feldolgozo megszakizasra kerult (a kiszolgalo leallitasa vagy idotullepes maitt) -> OperationCanceledException
            //   3) Vmi egyeb kivetel adodott a feldolgozoban
            //

            await task;
        }
    }

    /// <summary>
    /// Adds a timeout to the request processing.
    /// </summary>
    public class Timeout : RequestHandlerFactory
    {
        /// <summary>
        /// The request timeout.
        /// </summary>
        public TimeSpan Value { get; set; } = TimeSpan.FromSeconds(10);

        /// <inheritdoc/>
        protected override IRequestHandler Create(IRequestHandler next) => new TimeoutHandler(next, Value);
    }
}
