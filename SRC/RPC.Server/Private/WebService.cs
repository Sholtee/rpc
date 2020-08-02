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

namespace Solti.Utils.Rpc.Internals
{
    using Primitives.Patterns;
    using Properties;

    /// <summary>
    /// Implements a general Web Service over HTTP. 
    /// </summary>
    public class WebService: Disposable
    {
        #region Private
        private bool FNeedToRemoveUrlReservation;

        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "See https://docs.microsoft.com/en-us/dotnet/api/system.net.httplistener.system-idisposable-dispose?view=netcore-3.1#System_Net_HttpListener_System_IDisposable_Dispose")]
        private HttpListener? FListener;
        private Thread? FListenerThread;
        private readonly ManualResetEventSlim FTerminated = new ManualResetEventSlim(false);

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        private void Listen()
        {
            string category = $"[{nameof(HttpListener)}]";

            Trace.WriteLine($"Started on {Url}", category);

            Task isTerminated = Task.Factory.StartNew(FTerminated.Wait, TaskCreationOptions.LongRunning);

            Task<HttpListenerContext> getContext;

            do
            {
                getContext = FListener!.GetContextAsync();
                getContext.ContinueWith
                (
                    t => SafeCallContextProcessor(t.Result),  
                    TaskContinuationOptions.OnlyOnRanToCompletion
                );
            } while (Task.WaitAny(isTerminated, getContext) == 1);

            Trace.WriteLine($"Terminated on {Url}", category);
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
                throw new NotSupportedException();

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
                throw new Exception(Resources.NETSH_INVOCATION_FAILED);
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
        /// Calls the <see cref="Process(HttpListenerContext)"/> method in a safe manner.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        protected async virtual Task SafeCallContextProcessor(HttpListenerContext context) 
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            string category = $"[HTTP session {context.Request.RemoteEndPoint}]";

            Trace.WriteLine("Incoming request", category);

            try
            {
                SetAcHeaders(context);

                if (IsPreflight(context)) 
                {
                    Trace.WriteLine("Preflight request, processor won't be called", category);
                    context.Response.Close();
                    return;
                }

                Task processor = Process(context);

                if (await Task.WhenAny(processor, Task.Delay(Timeout)) != processor)
                    throw new TimeoutException();

                Trace.WriteLine("Request processed successfully", category);
            }
            catch(Exception ex)
            {
                Trace.WriteLine($"Request processing failed: {ex.Message}", category);

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
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// When overridden in the derived class it processes the incoming HTTP request.
        /// </summary>
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "'context' is never null")]
        protected virtual Task Process(HttpListenerContext context)
        {
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

                FTerminated.Dispose();
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
        [SuppressMessage("Design", "CA1056:Uri properties should not be strings")]
        public string? Url { get; private set; }

        /// <summary>
        /// Starts the Web Service.
        /// </summary>
        [SuppressMessage("Design", "CA1054:Uri parameters should not be strings")]
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

                if (Environment.OSVersion.Platform == PlatformID.Win32NT && ex is HttpListenerException httpEx && httpEx.ErrorCode == 5 /*ERROR_ACCESS_DENIED*/)
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

            FTerminated.Reset();

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

            FTerminated.Set();

            FListenerThread!.Join();
            FListenerThread = null;

            FListener!.Close();
            FListener = null;

            if (FNeedToRemoveUrlReservation)
                RemoveUrlReservation(Url!);

            Url = null;
        }

        /// <summary>
        /// Adds an URL reservation. For more information see http://msdn.microsoft.com/en-us/library/windows/desktop/cc307223(v=vs.85).aspx
        /// </summary>
        [SuppressMessage("Design", "CA1054:Uri parameters should not be strings")]
        public static void AddUrlReservation(string url) => InvokeNetsh($"http add urlacl url={url ?? throw new ArgumentNullException(nameof(url))} user=\"{Environment.UserDomainName}\\{Environment.UserName}\" listen=yes");

        /// <summary>
        /// Removes an URL reservation. For more information see http://msdn.microsoft.com/en-us/library/windows/desktop/cc307223(v=vs.85).aspx
        /// </summary>
        [SuppressMessage("Design", "CA1054:Uri parameters should not be strings")]
        public static void RemoveUrlReservation(string url) => InvokeNetsh($"http delete urlacl url={url ?? throw new ArgumentNullException(nameof(url))}");
        #endregion
    }
}