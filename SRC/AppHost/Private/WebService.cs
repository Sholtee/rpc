/********************************************************************************
* WebService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.AppHost.Internals
{
    using Primitives.Patterns;
    
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
            Debug.Assert(FListener != null);

            Task isTerminated = Task.Factory.StartNew(FTerminated.Wait, TaskCreationOptions.LongRunning);

            Task<HttpListenerContext> getContext;

            do
            {
                getContext = FListener!.GetContextAsync();
                getContext.ContinueWith
                (
                    (t, _) => SafeCallContextProcessor(t.Result), 
                    null, 
                    TaskContinuationOptions.OnlyOnRanToCompletion
                );
            } while (Task.WaitAny(isTerminated, getContext) == 1);
        }

        private static HttpListener CreateCore(string url) 
        {
            var result = new HttpListener();

            result.IgnoreWriteExceptions = true;

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

            Process.Start(psi)?.WaitForExit();
        }
        #endregion

        #region Protected
        /// <summary>
        /// Calls the <see cref="ProcessRequestContext(HttpListenerContext)"/> method in a safe manner.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        protected async virtual Task SafeCallContextProcessor(HttpListenerContext context) 
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                await ProcessRequestContext(context);
            }
            catch(Exception ex)
            {
                Trace.WriteLine($"{nameof(ProcessRequestContext)}() failed with error: {ex}");

                try
                {
                    HttpListenerResponse response = context.Response;

                    response.StatusCode = (int) HttpStatusCode.InternalServerError;
                    response.ContentType = "text/html";

                    await WriteResponseString(response, "Internal Server Error");

                    response.Close();
                }

                //
                // Ha menet kozben a kiszolgalo leallitasra kerult akkor a kivetelt megesszuk.
                //

                catch (ObjectDisposedException) { }
            }
        }

        /// <summary>
        /// Writes the given string into a <see cref="HttpListenerResponse"/>.
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
        protected virtual Task ProcessRequestContext(HttpListenerContext context)
        {
            context.Response.StatusCode = (int) HttpStatusCode.OK;
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
        /// Returns true if the Web Service has already been started (that does not mean that it is listening).
        /// </summary>
        public bool IsStarted => FListenerThread != null;

        /// <summary>
        /// Returns true if the Web Service is listening.
        /// </summary>
        public bool IsListening => FListener?.IsListening == true && FListenerThread?.IsAlive == true;

        /// <summary>
        /// The URL on which the Web Service is listaning.
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
            catch (HttpListenerException ex) 
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT && ex.ErrorCode == 5 /*ERROR_ACCESS_DENIED*/)
                {
                    AddUrlReservation(url);
                    FNeedToRemoveUrlReservation = true;

                    //
                    // Fasz se tudja miert de ha a Start() kivetelt dob akkor a HttpListener felszabaditasra kerul:
                    // https://github.com/dotnet/runtime/blob/0e870dfca57021542351a79983ad3ac1d289a23f/src/libraries/System.Net.HttpListener/src/System/Net/Windows/HttpListener.Windows.cs#L266
                    //

                    goto createcore;
                }

                //
                // Ha nem URL rezervacios gondunk volt akkor tovabb dobjuk a kivetelt
                //

                FListener = null;
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
        public static void AddUrlReservation(string url) => InvokeNetsh($"http add urlacl url={url} user=\"{Environment.UserDomainName}\\{Environment.UserName}\" listen=yes");

        /// <summary>
        /// Removes an URL reservation. For more information see http://msdn.microsoft.com/en-us/library/windows/desktop/cc307223(v=vs.85).aspx
        /// </summary>
        [SuppressMessage("Design", "CA1054:Uri parameters should not be strings")]
        public static void RemoveUrlReservation(string url) => InvokeNetsh($"http delete urlacl url={url}");
        #endregion
    }
}