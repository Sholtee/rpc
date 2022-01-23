/********************************************************************************
* ExceptionHandler.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

#pragma warning disable CA1031 // We have to catch all kind of exceptions here

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Properties;

    /// <summary>
    /// Catches unhandled exception thrown by the <see cref="Next"/> handler. 
    /// </summary>
    public class ExceptionCatcherHandler : IRequestHandler
    {
        /// <summary>
        /// Writes the given <paramref name="responseString"/> to the <paramref name="response"/>.
        /// </summary>
        protected async static Task WriteResponseString(IHttpResponse response, string responseString)
        {
            if (response is null)
                throw new ArgumentNullException(nameof(response));

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            await response.Payload.WriteAsync
            (
#if NETSTANDARD2_1_OR_GREATER
                buffer.AsMemory(0, buffer.Length)
#else
                buffer, 0, buffer.Length
#endif
            );
        }

        /// <summary>
        /// Processes unhandled exceptions.
        /// </summary>
        protected virtual async Task ProcessUnhandledException(Exception ex, IHttpSession context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                IHttpResponse response = context.Response;

                //
                // Http kivetelek megadhatjak a hiba kodjat.
                //

                response.StatusCode = (ex as HttpException)?.Status ?? HttpStatusCode.InternalServerError;

                if (!string.IsNullOrEmpty(ex.Message))
                {
                    response.Headers["Content-Type"] = "text/html";

                    //
                    // Itt ne hasznaljuk az context.Cancellation-t mivel lehet h pont a feldolgozo megszakitasa miatt kerultunk ide.
                    // Ilyen esetben a TaskCanceledException-t is gond nelkul szeretnenk feldolgozni.
                    //

                    await WriteResponseString(response, ex.Message);
                }

                await response.Close();
            }

            //
            // Ha menet kozben a kiszolgalo vmiert felszabaditasra kerult akkor a kivetelt megesszuk.
            //

            catch (ObjectDisposedException) { }
        }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// The parent instance.
        /// </summary>
        public ExceptionCatcher Parent { get; }

        /// <summary>
        /// Creates a new <see cref="ExceptionCatcherHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public ExceptionCatcherHandler(IRequestHandler next, ExceptionCatcher parent)
        {
            Next   = next   ?? throw new ArgumentNullException(nameof(next));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        /// <inheritdoc/>
        public async Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (context is null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                await Next.HandleAsync(scope, context, cancellation);
            }
            catch (Exception ex)
            {
                if (Parent.AllowLogs)
                    scope.TryGet<ILogger>()?.LogError(ex, Trace.REQUEST_PROCESSING_FAILED);

                await ProcessUnhandledException(ex, context);
            }
        }
    }

    /// <summary>
    /// Configures the request pipeline to be "exception proof".
    /// </summary>
    public class ExceptionCatcher : RequestHandlerFactory, ISupportsLog
    {
        /// <summary>
        /// If set to true, the <see cref="ILogger"/> service will be invoked in case of unhandled exception.
        /// </summary>
        public bool AllowLogs { get; set; } = true;

        /// <inheritdoc/>
        protected override IRequestHandler Create(IRequestHandler next) => new ExceptionCatcherHandler(next, this);
    }
}
