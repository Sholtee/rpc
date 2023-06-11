/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Primitives.Patterns;

    using TraceRes = Properties.Trace;

    /// <summary>
    /// Implements a general Web Service over HTTP. 
    /// </summary>
    public class WebService: Disposable
    {
        #region Private
        const int TERMINATED = -1;
        
        private int FActiveWorkers = TERMINATED;

        private readonly ManualResetEventSlim FTerminatedSignal = new();
        private readonly ManualResetEventSlim FStopSignal = new();

        private async Task CreateWorkerLoop()
        {
            int workerId = Interlocked.Increment(ref FActiveWorkers);

            //
            // At this point we cannot grab logger instances, so trace instead
            //

            Trace.WriteLine(string.Format(TraceRes.Culture, TraceRes.LISTENER_THREAD_STARTED, workerId));
            try
            {
                Task stopSignal = FStopSignal.AsTask();
                while (true)
                {
                    using CancellationTokenSource cts = new();

                    Task worker = DoWork(workerId, cts.Token);

                    if (await Task.WhenAny(worker, stopSignal) == stopSignal)
                        cts.Cancel();

                    //
                    // Use "await" even if we know that the worker was stopped successfully. Doing so will ensure that we
                    // won't eat any exceptions thrown by the "wroker".
                    //

                    await worker;
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Trace.TraceError(string.Format(TraceRes.Culture, TraceRes.EXCEPTION_IN_LISTENER_THREAD, workerId, ex));
            }
            finally
            {
                //
                // The last worker is supposed to notify the server about the successful termination.
                //

                if (Interlocked.Decrement(ref FActiveWorkers) is TERMINATED)
                    FTerminatedSignal.Set();
            }
            Trace.WriteLine(string.Format(TraceRes.Culture, TraceRes.LISTENER_THREAD_STOPPED, workerId));
        }
        #endregion

        #region Protected
        /// <inheritdoc/>
        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
            {
                if (IsStarted)
                    Stop().GetAwaiter().GetResult(); // TODO: szebben

                ScopeFactory.Dispose();

                FTerminatedSignal.Dispose();
                FStopSignal.Dispose();
            }

            base.Dispose(disposeManaged);
        }

        /// <inheritdoc/>
        protected override async ValueTask AsyncDispose()
        {
            if (IsStarted)
                await Stop();

            await ScopeFactory.DisposeAsync();

            FTerminatedSignal.Dispose();
            FStopSignal.Dispose();
        }

        /// <summary>
        /// Creates a new worker <see cref="Task"/> that waits for a new session then processes it.
        /// </summary>
        protected virtual async Task DoWork(int workerId, CancellationToken cancellation)
        {
            IHttpSession context = await HttpServer.WaitForSessionAsync(cancellation);

            await using IInjector scope = ScopeFactory.CreateScope(tag: context);

            ILogger? logger = scope.TryGet<ILogger>();

            DateTime started = DateTime.UtcNow;

            logger?.Info("WSVC-400", TraceRes.REQUEST_AVAILABLE, new
            {
                WrokerId = workerId,
                HttpServer.Url,
                RequestId = context.Request.Id,
                context.Request.RemoteEndPoint,
                Started = started
            });

            try
            {
                await scope.Get<IRequestHandler>().HandleAsync(scope, context, cancellation);

                logger?.Info("WSVC-401", TraceRes.REQUEST_PROCESSED, new
                {
                    WrokerId = workerId,
                    HttpServer.Url,
                    RequestId = context.Request.Id,
                    context.Request.RemoteEndPoint,
                    Took = (DateTime.UtcNow - started).TotalMilliseconds
                });
            }
            catch (Exception ex)
            {
                logger?.Error("WSVC-200", TraceRes.REQUEST_PROCESSING_FAILED, exception: ex, state: new
                {
                    WrokerId = workerId,
                    HttpServer.Url,
                    RequestId = context.Request.Id,
                    context.Request.RemoteEndPoint,
                    Took = (DateTime.UtcNow - started).TotalMilliseconds
                });

                if (!context.Response.IsClosed)
                {
                    context.Response.StatusCode = HttpStatusCode.InternalServerError;
                    await context.Response.Close();
                }
            }
        }
        #endregion

        #region Public
        /// <summary>
        /// Creates a new <see cref="WebService"/> instance.
        /// </summary>
        public WebService(IDiProvider diProvider, CancellationToken cancellation) // TBD: konstruktorba megszakitas???
        {
            if (diProvider is null)
                throw new ArgumentNullException(nameof(diProvider));

            ScopeFactory = diProvider.CreateFactory(cancellation);

            //
            // FIXME: In case of custom IScopeFactory implementation this cast may not work.
            //

            IInjector root = (IInjector) ScopeFactory;

            HttpServer = root.Get<IHttpServer>(); // singleton
        }

        /// <summary>
        /// Returns the <see cref="IScopeFactory"/> related to this instance.
        /// </summary>
        public IScopeFactory ScopeFactory { get; }

        /// <summary>
        /// The underlying <see cref="IHttpServer"/> implementation.
        /// </summary>
        protected IHttpServer HttpServer { get; }

        /// <summary>
        /// Starts the service.
        /// </summary>
        public Task Start()
        {
            if (HttpServer.IsStarted || FActiveWorkers is not TERMINATED)
                throw new InvalidOperationException();

            FStopSignal.Reset();
            FTerminatedSignal.Reset();

            FActiveWorkers = 0;

            HttpServer.Start();

            for (int i = 0; i < MaxWorkers; i++)
            {
                _ = CreateWorkerLoop();
            }

            Trace.TraceInformation(string.Format(TraceRes.Culture, TraceRes.SERVICE_STARTED));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public async Task Stop()
        {
            if (!HttpServer.IsStarted)
                throw new InvalidOperationException();

            //
            // Notify the workers to stop
            //

            FStopSignal.Set();

            //
            // Prevent new requests from being processed
            //

            HttpServer.Stop();

            if (Interlocked.Decrement(ref FActiveWorkers) is not TERMINATED)
                //
                // Wait while every workers stop
                //

                await FTerminatedSignal.AsTask();

            Trace.TraceInformation(string.Format(TraceRes.Culture, TraceRes.SERVICE_TERMINATED));
        }

        /// <summary>
        /// returns true if the server is started.
        /// </summary>
        public bool IsStarted => HttpServer.IsStarted;

        /// <summary>
        /// Returns true if the server is listening.
        /// </summary>
        public bool IsListening => FActiveWorkers > 0;

        /// <summary>
        /// The maximum number of worker threads.
        /// </summary>
        public int MaxWorkers { get; set; } = Environment.ProcessorCount;
        #endregion
    }
}