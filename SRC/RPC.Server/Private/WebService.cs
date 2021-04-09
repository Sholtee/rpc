/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;
    using Primitives.Patterns;
    using Properties;

    /// <summary>
    /// Implements a general Web Service over HTTP. 
    /// </summary>
    [SuppressMessage("Design", "CA1054:Uri parameters should not be strings")]
    [SuppressMessage("Design", "CA1056:Uri properties should not be strings")]
    public class WebService: Disposable
    {
        #region Private
        private bool FNeedToRemoveUrlReservation;

        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "See https://docs.microsoft.com/en-us/dotnet/api/system.net.httplistener.system-idisposable-dispose?view=netcore-3.1#System_Net_HttpListener_System_IDisposable_Dispose")]
        private HttpListener? FListener;
        private Thread? FListenerThread;
        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "This field is disposed correctly, see Stop() method")]
        private CancellationTokenSource? FListenerCancellation;

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        private void Listen()
        {
            ILogger? logger = LoggerFactory?.Invoke();

            using IDisposable? scope = logger?.BeginScope(new Dictionary<string, object>
            {
                [nameof(Url)] = Url!
            });

            logger?.LogInformation(Trace.SERVICE_STARTED);

            Task isTerminated = Task.Factory.StartNew(FListenerCancellation!.Token.WaitHandle.WaitOne, TaskCreationOptions.LongRunning);

            for (Task<HttpListenerContext> getContext; Task.WaitAny(isTerminated, getContext = FListener!.GetContextAsync()) == 1;) 
            {
                logger?.LogInformation(Trace.REQUEST_AVAILABLE);

                getContext.ContinueWith
                (
                    t => SafeCallContextProcessor(t.Result),
                    TaskContinuationOptions.OnlyOnRanToCompletion
                );
            }

            logger?.LogInformation(Trace.SERVICE_TERMINATED);
        }

        private static HttpListener CreateCore(string url) 
        {
            var result = new HttpListener
            {
                IgnoreWriteExceptions = true
            };

            try
            {
                result.Prefixes.Add(url);
            }
            catch
            {
                result.Close();
                throw;
            }

            return result;
        }

        private static void InvokeNetsh(string arguments) 
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException();

            var psi = new ProcessStartInfo("netsh", arguments)
            {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process netsh = System.Diagnostics.Process.Start(psi);
            
            netsh.WaitForExit();
            if (netsh.ExitCode != 0)
                #pragma warning disable CA2201 // Do not change the exception type to preserve backward compatibility
                throw new Exception(Errors.NETSH_INVOCATION_FAILED);
                #pragma warning restore CA2201
        }
        #endregion

        #region Protected
        /// <summary>
        /// Sets the "Access-Control-XxX" headers.
        /// </summary>
        protected virtual void SetAcHeaders(HttpListenerContext context) 
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            HttpListenerResponse response = context.Response;

            string? origin = context.Request.Headers.Get("Origin");

            if (!string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin))
            {
                response.Headers["Access-Control-Allow-Origin"] = origin;
                response.Headers["Vary"] = "Origin";
            }

            response.Headers["Access-Control-Allow-Methods"] = "*";
            response.Headers["Access-Control-Allow-Headers"] = "*";
        }

        /// <summary>
        /// Determines whether the request is a preflight request or not.
        /// </summary>
        protected static bool IsPreflight(HttpListenerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return context
                .Request
                .HttpMethod
                .Equals(HttpMethod.Options.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Calls the <see cref="Process(HttpListenerContext, ILogger?, CancellationToken)"/> method in a safe manner.
        /// </summary>
        protected async virtual Task SafeCallContextProcessor(HttpListenerContext context) 
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            ILogger? logger = LoggerFactory?.Invoke();

            using IDisposable? scope = logger?.BeginScope(new Dictionary<string, object> 
            {
                ["RequestId"]      = context.Request.RequestTraceIdentifier,
                ["RemoteEndPoint"] = context.Request.RemoteEndPoint,
                ["TimeStamp"]      = DateTime.UtcNow
            });

            try
            {
                logger?.LogInformation(Trace.BEGIN_REQUEST_PROCESSING);
                
                SetAcHeaders(context);

                if (IsPreflight(context)) 
                {
                    logger?.LogInformation(Trace.PREFLIGHT_REQUEST);
                    context.Response.Close();
                    return;
                }

                //
                // A feldolgozonak ket esetben kell leallnia:
                //   1) Adott idointervallumon belul nem sikerult a feladatat elvegeznie
                //   2) Maga a WebService kerul leallitasra
                //

                using CancellationTokenSource
                    processorCancellation = new CancellationTokenSource(),
                    cancel = CancellationTokenSource.CreateLinkedTokenSource(processorCancellation.Token, FListenerCancellation!.Token);

                Task processor = Process(context, logger, cancel.Token);

                if (await Task.WhenAny(processor, Task.Delay(Timeout)) != processor)
                    //
                    // Elkuldjuk a megszakitas kerelmet a feldolgozonak.
                    //

                    processorCancellation.Cancel();

                //
                // Itt a kovetkezo esetek lehetnek:
                //   1) A feldolgozo idoben befejezte a feladatat, az "await" mar nem fog varakozni, jok vagyunk
                //   2) A feldolgozo megszakizasra kerult (a kiszolgalo leallitasa vagy idotullepes maitt) -> OperationCanceledException
                //   3) Vmi egyeb kivetel adodott a feldolgozoban
                //

                await processor;

                logger?.LogInformation(Trace.REQUEST_PROCESSED);
            }
            #pragma warning disable CA1031 // We have to catch all kind of exceptions here
            catch (Exception ex)
            #pragma warning restore CA1031
            {
                logger?.LogError(ex, Trace.REQUEST_PROCESSING_FAILED);

                await ProcessUnhandledException(ex, context);
            }
        }

        /// <summary>
        /// Processes exceptions unhandled by user code.
        /// </summary>
        protected virtual async Task ProcessUnhandledException(Exception ex, HttpListenerContext context) 
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                HttpListenerResponse response = context.Response;

                //
                // Http kivetelek megadhatjak a hiba kodjat.
                //

                response.StatusCode = (int)((ex as HttpException)?.Status ?? HttpStatusCode.InternalServerError);

                if (!string.IsNullOrEmpty(ex.Message))
                {
                    response.ContentType = "text/html";
                    await WriteResponseString(response, ex.Message);
                }

                response.Close();
            }

            //
            // Ha menet kozben a kiszolgalo leallitasra kerult akkor a kivetelt megesszuk.
            //

            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Writes the given <paramref name="responseString"/> to the <paramref name="response"/>.
        /// </summary>
        protected async static Task WriteResponseString(HttpListenerResponse response, string responseString) 
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync
            (
#if NETSTANDARD2_0
                buffer, 0, buffer.Length
#else
                buffer.AsMemory(0, buffer.Length)
#endif
            );
        }

        /// <summary>
        /// When overridden in the derived class it processes the incoming HTTP request.
        /// </summary>
        #pragma warning disable CS3001 // ILogger is not CLS-compliant
        protected virtual Task Process(HttpListenerContext context, ILogger? logger, CancellationToken cancellation)
        #pragma warning restore CS3001
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.Response.StatusCode = (int) HttpStatusCode.NoContent;
            context.Response.Close();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispsoes this instance.
        /// </summary>
        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
            {
                if (IsStarted)
                    Stop();
            }

            base.Dispose(disposeManaged);
        }
        #endregion

        #region Public
        /// <summary>
        /// Returns true if the Web Service has already been started (which does not imply that it <see cref="IsListening"/>).
        /// </summary>
        public bool IsStarted => FListenerThread != null;

        /// <summary>
        /// Returns true if the Web Service is listening.
        /// </summary>
        public bool IsListening => FListener?.IsListening == true && FListenerThread?.IsAlive == true;

        /// <summary>
        /// The maximum amount of time that is available for the service to serve the request.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Cross-origin_resource_sharing
        /// </summary>
        public ICollection<string> AllowedOrigins { get; } = new List<string>();

        /// <summary>
        /// The URL on which the Web Service is listening.
        /// </summary>  
        public string? Url { get; private set; }

        /// <summary>
        /// If set it defines the delegate that will be used for creating logger instances. 
        /// </summary>
        /// <remarks>Every session will have its own logger instance.</remarks>
        #pragma warning disable CS3003 // ILogger is not CLS-compliant
        public Func<ILogger>? LoggerFactory { get; set; } = () => TraceLogger.Create<WebService>();
        #pragma warning restore CS3003

        /// <summary>
        /// Starts the Web Service.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void Start(string url)
        {
            if (IsStarted)
                return;

            createcore:
            FListener = CreateCore(url);

            try
            {
                FListener.Start();
            }
            catch (Exception ex) 
            {
                //
                // Fasz se tudja miert de ha a Start() kivetelt dob akkor a HttpListener felszabaditasra kerul:
                // https://github.com/dotnet/runtime/blob/0e870dfca57021542351a79983ad3ac1d289a23f/src/libraries/System.Net.HttpListener/src/System/Net/Windows/HttpListener.Windows.cs#L266
                //

                FListener = null;

                #pragma warning disable CA1508 // There is not dead conditional code
                if (Environment.OSVersion.Platform == PlatformID.Win32NT && ex is HttpListenerException httpEx && httpEx.ErrorCode == 5 /*ERROR_ACCESS_DENIED*/ && !FNeedToRemoveUrlReservation)
                #pragma warning restore CA1508
                {
                    AddUrlReservation(url);
                    FNeedToRemoveUrlReservation = true;

                    //
                    // Megprobaljuk ujra letrehozni.
                    //

                    goto createcore;
                }

                //
                // Ha nem URL rezervacios gondunk volt akkor tovabb dobjuk a kivetelt
                //

                throw;
            }

            FListenerCancellation = new CancellationTokenSource();

            FListenerThread = new Thread(Listen);
            FListenerThread.Start();

            Url = url;
        }

        /// <summary>
        /// Shuts down the Web Service.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        public virtual void Stop()
        {
            if (!IsStarted) return;

            FListenerCancellation!.Cancel();

            FListenerThread!.Join();
            FListenerThread = null;

            FListener!.Close();
            FListener = null;

            //
            // Lehet Dispose()-olni mert a feldolgozok nem kozvetlenul hivatkozzak.
            //

            FListenerCancellation.Dispose();
            FListenerCancellation = null;

            if (FNeedToRemoveUrlReservation)
            {
                try
                {
                    RemoveUrlReservation(Url!);
                }
                #pragma warning disable CA1031 // This method should not throw
                catch { }
                #pragma warning restore CA1031

                FNeedToRemoveUrlReservation = false;
            }

            Url = null;
        }

        /// <summary>
        /// Adds an URL reservation. For more information see http://msdn.microsoft.com/en-us/library/windows/desktop/cc307223(v=vs.85).aspx
        /// </summary>        
        public static void AddUrlReservation(string url) => InvokeNetsh($"http add urlacl url={url ?? throw new ArgumentNullException(nameof(url))} user=\"{Environment.UserDomainName}\\{Environment.UserName}\" listen=yes");

        /// <summary>
        /// Binds an SSL certificate to the given IP and port.
        /// </summary>
        public static void AddSslCert(IPEndPoint ipPort, string certHash) => InvokeNetsh($"http add sslcert ipport={ipPort ?? throw new ArgumentNullException(nameof(ipPort))} certhash={certHash ?? throw new ArgumentNullException(nameof(certHash))} appid={Guid.NewGuid().ToString("B")}");

        /// <summary>
        /// Removes an URL reservation. For more information see http://msdn.microsoft.com/en-us/library/windows/desktop/cc307223(v=vs.85).aspx
        /// </summary>
        public static void RemoveUrlReservation(string url) => InvokeNetsh($"http delete urlacl url={url ?? throw new ArgumentNullException(nameof(url))}");

        /// <summary>
        /// Removes the bound SSL certificate.
        /// </summary>
        public static void RemoveSslCert(IPEndPoint ipPort) => InvokeNetsh($"http delete sslcert ipport={ipPort ?? throw new ArgumentNullException(nameof(ipPort))}");
        #endregion
    }
}