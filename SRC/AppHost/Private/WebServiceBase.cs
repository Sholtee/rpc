/********************************************************************************
* WebServiceBase.cs                                                             *
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
    public class WebServiceBase: Disposable
    {
        private bool FNeedToRemoveUrlReservation;

        private HttpListener FListener = new HttpListener();

        #region Protected
        /// <summary>
        /// Processes new requests.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        protected virtual void Listen()
        {
            using AutoResetEvent nextTurn = new AutoResetEvent(false);

            while (IsListening)
            {
                FListener.BeginGetContext(asyncResult =>
                {
                    HttpListenerContext? context = null;

                    try
                    {
                        //
                        // Blokkolodik amig nincs adat.
                        //

                        context = FListener.EndGetContext(asyncResult);
                    }
                    catch(Exception ex)
                    {
                        //
                        // Ha az FListener.Stop() hivva volt amig az EndGetContext() varakozott akkor ide jutunk
                        //

                        Trace.WriteLine($"{nameof(FListener)}.{nameof(FListener.EndGetContext)} failed with error: {ex}");
                    }

                    //
                    // Ha a kiszolgalo leallitasra kerult (catch) v sikeresen lekerdeztuk a kontextust akkor jelezzuk
                    // h a "while" ciklus a kovetkezo iteracioba lephet (ha tud).
                    //

                    nextTurn.Set();

                    if (context != null) SafeCallContextProcessor(context);
                }, null);

                nextTurn.WaitOne();
            }
        }

        /// <summary>
        /// Calls the <see cref="ProcessRequestContext(HttpListenerContext)"/> method in a safe manner.
        /// </summary>
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "'context' is never null"), SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        protected virtual void SafeCallContextProcessor(HttpListenerContext context) 
        {
            try
            {
                ProcessRequestContext(context).GetAwaiter().GetResult();
            }
            catch
            {
                //
                // Ha nem a kiszolgalo leallitas miatt volt a kivetel akkor HTTP 500
                //

                if (IsListening)
                {
                    HttpListenerResponse response = context.Response;

                    response.StatusCode = (int) HttpStatusCode.InternalServerError;
                    response.ContentType = "text/html";

                    WriteResponseString(response, "Internal Server Error");

                    response.Close();
                }
            }
        }

        /// <summary>
        /// Writes the given string into a <see cref="HttpListenerResponse"/>.
        /// </summary>
        protected static void WriteResponseString(HttpListenerResponse response, string responseString) 
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
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

                //
                // Megvalisotja az IDisposable-t csak explicit-en
                //

                (FListener as IDisposable)?.Dispose();
            }

            base.Dispose(disposeManaged);
        }
        #endregion

        #region Public
        /// <summary>
        /// Returns true if the Web Service has already been started (that does not mean that it is listening).
        /// </summary>
        public bool IsStarted => Url != null;

        /// <summary>
        /// Returns true if the Web Service is listening.
        /// </summary>
        public bool IsListening => FListener.IsListening;


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
        public void Start(string url)
        {
            if (IsStarted)
                return;

            FListener.Prefixes.Add(url);

            try
            {
                FListener.Start();
            }
            catch (HttpListenerException ex) 
            {
                //
                // Fasz se tudja miert de ha kivetel volt akkor a HttpListener felszabaditasra kerul:
                // https://github.com/dotnet/runtime/blob/0e870dfca57021542351a79983ad3ac1d289a23f/src/libraries/System.Net.HttpListener/src/System/Net/Windows/HttpListener.Windows.cs#L266
                //

                EnsureListenerNotDisposed();

                if (Environment.OSVersion.Platform == PlatformID.Win32NT && ex.ErrorCode == 5 /*ERROR_ACCESS_DENIED*/)
                {
                    AddUrlReservation(url);
                    FNeedToRemoveUrlReservation = true;
                    FListener.Start();
                }

                //
                // Ha nem URL rezervacios gondunk volt akkor tovabb dobjuk a kivetelt
                //

                else throw;
            }

            ThreadPool.QueueUserWorkItem(_ => Listen());

            Url = url;

            void EnsureListenerNotDisposed() 
            {
                try
                {
                    FListener.Stop();
                }
                catch (ObjectDisposedException)
                {
                    FListener = new HttpListener();
                    FListener.Prefixes.Add(url);
                }
            }
        }

        /// <summary>
        /// Shuts down the Web Service.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (!IsStarted) return;

            FListener.Stop();
            FListener.Prefixes.Remove(Url);

            if (FNeedToRemoveUrlReservation)
                RemoveUrlReservation(Url!);

            Url = null;
        }

        /// <summary>
        /// Adds an URL reservation. For more information see http://msdn.microsoft.com/en-us/library/windows/desktop/cc307223(v=vs.85).aspx
        /// </summary>
        [SuppressMessage("Design", "CA1054:Uri parameters should not be strings")]
        public static void AddUrlReservation(string url)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;

            var psi = new ProcessStartInfo("netsh", $"http add urlacl url={url} user=\"{Environment.UserDomainName}\\{Environment.UserName}\" listen=yes")
            {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process.Start(psi)?.WaitForExit();
        }

        /// <summary>
        /// Removes an URL reservation. For more information see http://msdn.microsoft.com/en-us/library/windows/desktop/cc307223(v=vs.85).aspx
        /// </summary>
        [SuppressMessage("Design", "CA1054:Uri parameters should not be strings")]
        public static void RemoveUrlReservation(string url)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;

            var psi = new ProcessStartInfo("netsh", $"http delete urlacl url={url}")
            {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process.Start(psi)?.WaitForExit();
        }
        #endregion
    }
}