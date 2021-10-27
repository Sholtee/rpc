/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;
    using Interfaces;
    using Primitives.Patterns;
    using Primitives.Threading;
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

        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "See https://docs.microsoft.com/en-us/dotnet/api/system.net.httplistener.system-idisposable-dispose?view=netcore-3.1#remarks")]
        private HttpListener? FListener;
        private Thread? FListenerThread;
        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "This field is disposed correctly, see Stop() method")]
        private CancellationTokenSource? FListenerCancellation;

        private readonly ExclusiveBlock FExclusiveBlock = new();

        private int FActiveRequests;

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        private void Listen()
        {
            try
            {
                //
                // Ez az injector csak a listener thread-hez tartozik, ezen a metoduson kivul TILOS hasznalni.
                //

                using IInjector injector = ScopeFactory.CreateScope();

                ILogger? logger = injector.TryGet<ILogger>();

                //
                // Nem gond ha "logScope" NULL, nem lesz kivetel a using blokk vegen:
                // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#the-using-statement
                //

                using IDisposable? logScope = logger?.BeginScope(new Dictionary<string, object>
                {
                    [nameof(Descriptor.Url)] = Descriptor.Url
                });

                logger?.LogInformation(Trace.SERVICE_STARTED);

                do
                {
                    Task<HttpListenerContext> getContext = FListener!.GetContextAsync();

                    if (WaitHandle.WaitAny(new WaitHandle[] { FListenerCancellation!.Token.WaitHandle, ((IAsyncResult) getContext).AsyncWaitHandle }) == 0)
                        break;

                    switch (getContext.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            logger?.LogInformation(Trace.REQUEST_AVAILABLE);

                            Interlocked.Increment(ref FActiveRequests);
                            try
                            {
                                //
                                // Uj Task-ban hivjuk a feldolgozot.
                                //

                                #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                SafeCallContextProcessor(getContext.Result);
                                #pragma warning restore CS4014
                            }
                            finally
                            {
                                Interlocked.Decrement(ref FActiveRequests);
                            }
                            break;
                        case TaskStatus.Faulted:
                            logger?.LogError(getContext.Exception.ToString());
                            break;
                    }
                } while (true);

                logger?.LogInformation(Trace.SERVICE_TERMINATED);
            }
            #pragma warning disable CA1031 // We have to catch all kind of exceptions here
            catch (Exception ex)
            #pragma warning restore CA1031
            {
                System.Diagnostics.Trace.WriteLine(string.Format(Trace.Culture, Trace.EXCEPTION_IN_LISTENER_THREAD, ex));
            }
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
            if (Environment.OSVersion.Platform is not PlatformID.Win32NT)
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

        private async Task AddTimeout(Func<CancellationToken, Task> fn)
        {
            //
            // A feldolgozonak ket esetben kell leallnia:
            //   1) Adott idointervallumon belul nem sikerult a feladatat elvegeznie
            //   2) Maga a WebService kerul leallitasra
            //

            using CancellationTokenSource
                taskCancellation = new(),
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(taskCancellation.Token, FListenerCancellation!.Token);

            Task task = fn(linkedCancellation.Token);

            if (await Task.WhenAny(task, Task.Delay(Descriptor.Timeout)) != task)
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
        #endregion

        #region Protected
        /// <summary>
        /// Sets the "Access-Control-XxX" headers.
        /// </summary>
        /// <remarks>This method may be called parallelly.</remarks>
        protected virtual void SetAcHeaders(HttpListenerContext context) 
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            HttpListenerResponse response = context.Response;

            string? origin = context.Request.Headers.Get("Origin");

            if (!string.IsNullOrEmpty(origin) && Descriptor.AllowedOrigins.Contains(origin))
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
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return context
                .Request
                .HttpMethod
                .Equals(HttpMethod.Options.ToString(), StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Calls the <see cref="Process(HttpListenerContext, IInjector, CancellationToken)"/> method in a safe manner.
        /// </summary>
        /// <remarks>This method may be called parallelly.</remarks>
        protected async virtual Task SafeCallContextProcessor(HttpListenerContext context) 
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            await using IInjector injector = ScopeFactory.CreateScope();

            ILogger? logger = injector.TryGet<ILogger>();

            //
            // Nem gond ha "logScope" NULL, nem lesz kivetel a using blokk vegen:
            // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#the-using-statement
            //

            using IDisposable? logScope = logger?.BeginScope(new Dictionary<string, object> 
            {
                ["RequestId"] = context.Request.RequestTraceIdentifier,
                ["RemoteEndPoint"] = context.Request.RemoteEndPoint,
                ["TimeStamp"] = DateTime.UtcNow
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

                await AddTimeout(cancel => Process(context, injector, cancel));

                logger?.LogInformation(Trace.REQUEST_PROCESSED);
            }
            #pragma warning disable CA1031 // We have to catch all kind of exceptions here
            catch (Exception ex)
            #pragma warning restore CA1031
            {
                logger?.LogError(ex, Trace.REQUEST_PROCESSING_FAILED);

                await AddTimeout(cancel => ProcessUnhandledException(ex, context, injector, cancel));
            }
        }

        /// <summary>
        /// Processes exceptions unhandled by user code.
        /// </summary>
        /// <remarks>This method may be called parallelly.</remarks>
        protected virtual async Task ProcessUnhandledException(Exception ex, HttpListenerContext context, IInjector injector, CancellationToken cancellation) 
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
                    // Itt ne hasznaljuk az FListenerCancellation-t mivel lehet h pont a feldolgozo megszakitasa miatt kerultunk ide. Ilyen
                    // esetben a TaskCanceledException-t is gond nelkul szeretnenk feldolgozni.
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
        /// When overridden in the derived class it processes the incoming HTTP request.
        /// </summary>
        /// <remarks>This method may be called parallelly.</remarks>
        protected virtual Task Process(HttpListenerContext context, IInjector injector, CancellationToken cancellation)
        {
            if (context is null)
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
                ScopeFactory.Dispose();
                FExclusiveBlock.Dispose();
            }

            base.Dispose(disposeManaged);
        }

        /// <summary>
        /// Creates a new <see cref="WebService"/> instance.
        /// </summary>
        protected WebService(WebServiceDescriptor descriptor, IScopeFactory scopeFactory)
        {
            ScopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }
        #endregion

        #region Public
        /// <summary>
        /// Creates a new <see cref="WebService"/> instance.
        /// </summary>
        public WebService(WebServiceDescriptor descriptor) : this(descriptor, DI.ScopeFactory.Create(svcs => svcs.Factory<ILogger>(i => TraceLogger.Create<WebService>(), Lifetime.Scoped))) { }

        /// <summary>
        /// Returns true if the Web Service has already been started (which does not imply that it <see cref="IsListening"/>).
        /// </summary>
        public bool IsStarted => FListenerThread is not null;

        /// <summary>
        /// Returns true if the Web Service is listening.
        /// </summary>
        public bool IsListening => FListener?.IsListening is true && FListenerThread?.IsAlive is true;

        /// <summary>
        /// Returns the <see cref="WebServiceDescriptor"/> related to this instance.
        /// </summary>
        public WebServiceDescriptor Descriptor { get; }

        /// <summary>
        /// Returns the <see cref="IScopeFactory"/> related to this instance.
        /// </summary>
        public IScopeFactory ScopeFactory { get; }

        /// <summary>
        /// Starts the Web Service.
        /// </summary>
        public void Start()
        {
            using (FExclusiveBlock.Enter())
            {
                if (IsStarted)
                    return;

                createcore:
                FListener = CreateCore(Descriptor.Url);

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

                    #pragma warning disable CA1508 // There is no dead conditional code
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT && ex is HttpListenerException httpEx && httpEx.ErrorCode == 5 /*ERROR_ACCESS_DENIED*/ && !FNeedToRemoveUrlReservation)
                    #pragma warning restore CA1508
                    {
                        AddUrlReservation(Descriptor.Url);
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
            }
        }

        /// <summary>
        /// Shuts down the Web Service.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        public void Stop() => Stop(TimeSpan.FromMinutes(2));

        /// <summary>
        /// Shuts down the Web Service.
        /// </summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
        public void Stop(TimeSpan timeout)
        {
            using (FExclusiveBlock.Enter())
            {
                if (!IsStarted)
                    return;

                //
                // Nem fogadunk tobb kerest valamint jelezzuk a meg aktiv feldolgozoknak h alljanak le.
                //

                FListenerCancellation!.Cancel();

                FListenerThread!.Join(); // ez utan mar biztosan nem kezdodik ujabb keres-feldolgozas
                FListenerThread = null;

                //
                // Mielott magat a mogottes kiszolgalot leallitanak megvarjuk amig minden meg elo keres lezarasra kerul.
                //

                SpinWait.SpinUntil(() => FActiveRequests == 0, timeout);

                //
                // Kiszolgalo leallitasa.
                //

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
                        RemoveUrlReservation(Descriptor.Url);
                    }
                    #pragma warning disable CA1031 // This method should not throw
                    catch { }
                    #pragma warning restore CA1031

                    FNeedToRemoveUrlReservation = false;
                }
            }
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