/********************************************************************************
* ExceptionHandler.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

#pragma warning disable CA1031 // We have to catch all kind of exceptions here

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Properties;

    /// <summary>
    /// Processes unhandled exceptions.
    /// </summary>
    public class CatchAllExceptionsHandler : IRequestHandler
    {
        /// <summary>
        /// Writes the given <paramref name="responseString"/> to the <paramref name="response"/>.
        /// </summary>
        protected async static Task WriteResponseString(HttpListenerResponse response, string responseString)
        {
            if (response is null)
                throw new ArgumentNullException(nameof(response));

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync
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
        protected virtual async Task ProcessUnhandledException(Exception ex, RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                HttpListenerResponse response = context.Response;

                //
                // Http kivetelek megadhatjak a hiba kodjat.
                //

                response.StatusCode = (int) ((ex as HttpException)?.Status ?? HttpStatusCode.InternalServerError);

                if (!string.IsNullOrEmpty(ex.Message))
                {
                    response.ContentType = "text/html";

                    //
                    // Itt ne hasznaljuk az context.Cancellation-t mivel lehet h pont a feldolgozo megszakitasa miatt kerultunk ide.
                    // Ilyen esetben a TaskCanceledException-t is gond nelkul szeretnenk feldolgozni.
                    //

                    await WriteResponseString(response, ex.Message);
                }

                response.Close();
            }

            //
            // Ha menet kozben a kiszolgalo vmiert felszabaditasra kerult akkor a kivetelt megesszuk.
            //

            catch (ObjectDisposedException) { }
        }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// Creates a new <see cref="CatchAllExceptionsHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public CatchAllExceptionsHandler(IRequestHandler next) => Next = next ?? throw new ArgumentNullException(nameof(next));

        /// <inheritdoc/>
        public async Task HandleAsync(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                await Next.HandleAsync(context);
            }
            catch (Exception ex)
            {
                context.Scope.TryGet<ILogger>()?.LogError(ex, Trace.REQUEST_PROCESSING_FAILED);

                await ProcessUnhandledException(ex, context);
            }
        }
    }

    /// <summary>
    /// Catches unhandled exceptions.
    /// </summary>
    public class ExceptionCatcher : RequestHandlerFactory
    {
        /// <inheritdoc/>
        protected override IRequestHandler Create(IRequestHandler next) => new CatchAllExceptionsHandler(next);
    }
}
