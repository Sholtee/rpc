/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

#pragma warning disable CA1031 // Do not catch general exception types

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
            // Mindenkepp Trace-re keruljon.
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
                    // Meg ha tudjuk is hogy a "worker" befejezodott akkor is legyen "await" hogy ha kivetel
                    // volt akkor azt tovabb tudjuk dobni.
                    //

                    await worker;
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Trace.WriteLine(string.Format(TraceRes.Culture, TraceRes.EXCEPTION_IN_LISTENER_THREAD, workerId, ex));
            }
            finally
            {
                //
                // Leallitaskor is dekrementaljuk az FActiveRequests valtozot, igy ha az negativ erteket er el akkor az utolso
                // feldolgozo fogja kikuldeni az ertesitest a sikeres leallasrol.
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

            await using IInjector scope = ScopeFactory.CreateScope();

            //
            // Mivel nem tudjuk hogy a naplo szerviz szalbiztos e ezert minden munkafolyamat sajat peldanyt ker
            //

            ILogger? logger = scope.TryGet<ILogger>();

            //
            // Nem gond ha "logScope" NULL, nem lesz kivetel a using blokk vegen:
            // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#the-using-statement
            //

            using IDisposable? logScope = logger?.BeginScope(new Dictionary<string, object>
            {
                ["Worker ID"] = workerId,
                ["Url"] = HttpServer.Url,
                ["Remote EndPoint"] = context.Request.RemoteEndPoint
            });

            logger?.LogInformation(TraceRes.REQUEST_AVAILABLE);

            try
            {
                await scope.Get<IRequestHandler>().HandleAsync(scope, context, cancellation);

                logger?.LogInformation(TraceRes.REQUEST_PROCESSED);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, TraceRes.REQUEST_PROCESSING_FAILED);

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
        public WebService(IServiceCollection serviceCollection, ScopeOptions? scopeOptions, CancellationToken cancellation)
        {
            ScopeFactory = DI.ScopeFactory.Create(serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection)), scopeOptions, cancellation);

            IInjector root = (IInjector) ScopeFactory;

            HttpServer = root.Get<IHttpServer>(); // singleton
            Logger = root.TryGet<ILogger>();
        }

        /// <summary>
        /// Returns the <see cref="IScopeFactory"/> related to this instance.
        /// </summary>
        public IScopeFactory ScopeFactory { get; protected init; }  // init kell, hogy leszarmazottban is beallithato legyen

        /// <summary>
        /// The underlying <see cref="IHttpServer"/> implementation.
        /// </summary>
        protected IHttpServer HttpServer { get; init; }

        /// <summary>
        /// The logger associated with this instance.
        /// </summary>
        /// <remarks>This logger belongs to the service itself, not intended to be used in worker threads.</remarks>
        protected ILogger? Logger { get; init; } 

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

            Logger?.LogInformation(TraceRes.SERVICE_STARTED);

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
            // Workerek ertesitese hogy alljanak le.
            //

            FStopSignal.Set();

            //
            // Ujabb kereseket mar nem fogadunk.
            //

            HttpServer.Stop();

            if (Interlocked.Decrement(ref FActiveWorkers) is not TERMINATED)
                //
                // Megvarjuk amig mindenki leall
                //

                await FTerminatedSignal.AsTask();

            Logger?.LogInformation(TraceRes.SERVICE_TERMINATED);
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