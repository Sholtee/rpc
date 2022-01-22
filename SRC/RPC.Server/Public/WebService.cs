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

            ILogger logger = TraceLogger.Create<WebService>();

            using IDisposable logScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["Worker ID"] = workerId,
            });

            logger.LogInformation(TraceRes.LISTENER_THREAD_STARTED);     
            try
            {
                Task stopSignal = FStopSignal.AsTask();
                while (true)
                {
                    using CancellationTokenSource cts = new();

                    Task worker = DoWork(cts.Token);

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
                    logger.LogError(ex, TraceRes.EXCEPTION_IN_LISTENER_THREAD);
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
            logger.LogInformation(TraceRes.LISTENER_THREAD_STOPPED);
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
        protected virtual async Task DoWork(CancellationToken cancellation)
        {
            await using IInjector scope = ScopeFactory.CreateScope();

            IHttpServer server = scope.Get<IHttpServer>(); // singleton

            IHttpSession context = await server.WaitForSessionAsync(cancellation);

            ILogger? logger = scope.TryGet<ILogger>();

            //
            // Nem gond ha "logScope" NULL, nem lesz kivetel a using blokk vegen:
            // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#the-using-statement
            //

            using IDisposable? logScope = logger?.BeginScope(new Dictionary<string, object>
            {
                ["Url"] = server.Url,
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
        public WebService(IServiceCollection serviceCollection) => ScopeFactory = DI.ScopeFactory.Create(serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection)));

        /// <summary>
        /// Returns the <see cref="IScopeFactory"/> related to this instance.
        /// </summary>
        public IScopeFactory ScopeFactory { get; protected init; }

        /// <summary>
        /// Starts the service.
        /// </summary>
        public async Task Start()
        {
            await using IInjector scope = ScopeFactory.CreateScope();

            IHttpServer server = scope.Get<IHttpServer>(); // singleton
            if (server.IsStarted || FActiveWorkers > TERMINATED)
                throw new InvalidOperationException();

            FStopSignal.Reset();
            FTerminatedSignal.Reset();

            FActiveWorkers = 0;

            server.Start();

            for (int i = 0; i < MaxWorkers; i++)
            {
                _ = CreateWorkerLoop();
            }
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public async Task Stop()
        {
            await using IInjector scope = ScopeFactory.CreateScope();

            IHttpServer server = scope.Get<IHttpServer>(); // singleton
            if (!server.IsStarted)
                throw new InvalidOperationException();

            //
            // Ujabb kereseket mar nem fogadunk.
            //

            server.Stop();

            FStopSignal.Set();

            if (Interlocked.Decrement(ref FActiveWorkers) is TERMINATED)
                return;

            //
            // Megvarjuk amig mindenki leall
            //

            await FTerminatedSignal.AsTask();
        }

        /// <summary>
        /// returns true if the server is started.
        /// </summary>
        public bool IsStarted
        {
            get
            {
                using IInjector scope = ScopeFactory.CreateScope();
                try
                {
                    return scope.Get<IHttpServer>().IsStarted; // singleton
                } catch { return false; }
            }
        }

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