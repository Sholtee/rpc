/********************************************************************************
* ExceptionHandler.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Properties;

    /// <summary>
    /// Specifies the <see cref="ExceptionCatcherHandler"/> configuration.
    /// </summary>
    public interface IExceptionCatcherHandlerConfig
    {
        /// <summary>
        /// Returns true if the logging is allowed.
        /// </summary>
        public bool AllowLogs { get; }
    }

    /// <summary>
    /// Catches unhandled exception thrown by the encapsulated handler. 
    /// </summary>
    public class ExceptionCatcherHandler : RequestHandlerBase<IExceptionCatcherHandlerConfig>
    {
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
                    //
                    // Itt ne hasznaljuk az context.Cancellation-t mivel lehet h pont a feldolgozo megszakitasa miatt kerultunk ide.
                    // Ilyen esetben a TaskCanceledException-t is gond nelkul szeretnenk feldolgozni.
                    //

                    await response.WriteResponseString(ex.Message);

                await response.Close();
            }

            //
            // Ha menet kozben a kiszolgalo vmiert felszabaditasra kerult akkor a kivetelt megesszuk.
            //

            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Creates a new <see cref="ExceptionCatcherHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public ExceptionCatcherHandler(IRequestHandler next, IExceptionCatcherHandlerConfig config) :base(next, config) { }

        /// <inheritdoc/>
        public override async Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
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
                if (Config.AllowLogs)
                    scope.TryGet<ILogger>()?.Error("EXHA-200", Trace.REQUEST_PROCESSING_FAILED, exception: ex);

                await ProcessUnhandledException(ex, context);
            }
        }
    }

    /// <summary>
    /// Configures the request pipeline to be "exception proof".
    /// </summary>
    public class ExceptionCatcher : RequestHandlerBuilder, IExceptionCatcherHandlerConfig
    {
        /// <summary>
        /// Creates a new <see cref="ExceptionCatcher"/> instance.
        /// </summary>
        public ExceptionCatcher(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent) : base(webServiceBuilder, parent) { }

        /// <summary>
        /// If set to true, the <see cref="ILogger"/> service will be invoked in case of unhandled exception.
        /// </summary>
        public bool AllowLogs { get; set; } = true;

        /// <inheritdoc/>
        public override IRequestHandler Build(IRequestHandler next) => new ExceptionCatcherHandler(next, this);
    }
}
