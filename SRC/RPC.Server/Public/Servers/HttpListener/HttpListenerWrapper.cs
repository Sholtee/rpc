/********************************************************************************
* HttpListenerWrapper.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1054 // URI-like parameters should not be strings

namespace Solti.Utils.Rpc.Servers
{
    using Interfaces;
    using Internals;
    using Primitives.Patterns;

    using TraceRes = Properties.Trace;
    using ErrorRes = Properties.Errors;

    /// <summary>
    /// Implements a general Web Service over HTTP. 
    /// </summary>
    public class HttpListenerWrapper : Disposable, IHttpServer
    {
        #region Private
        private sealed class HttpListenerRequestWrapper : IHttpRequest
        {
            public HttpListenerRequestWrapper(HttpListenerRequest originalRequest)
            {
                Headers         = new NameValueCollectionWrapper(originalRequest.Headers);
                QueryParameters = new NameValueCollectionWrapper(originalRequest.QueryString);
                Method          = originalRequest.HttpMethod;
                Payload         = originalRequest.InputStream;
                RemoteEndPoint  = originalRequest.RemoteEndPoint;
                OriginalRequest = originalRequest;
            }

            public IReadOnlyDictionary<string, string> Headers { get; }

            public IReadOnlyDictionary<string, string> QueryParameters { get; }

            public string Method { get; }

            public Stream Payload { get; }

            public IPEndPoint RemoteEndPoint { get; }

            public object OriginalRequest { get; }
        }

        private sealed class HttpListenerResponseWrapper : IHttpResponse
        {
            public HttpListenerResponseWrapper(HttpListenerResponse originalResponse)
            {
                OriginalResponse = originalResponse;
                Headers = new NameValueCollectionWrapper(originalResponse.Headers);
            }

            public HttpListenerResponse OriginalResponse { get; }

            public HttpStatusCode StatusCode { get => (HttpStatusCode) OriginalResponse.StatusCode; set => OriginalResponse.StatusCode = (int) value; }

            public Stream Payload => OriginalResponse.OutputStream;

            public IDictionary<string, string> Headers { get; }

            public bool IsClosed { get; private set; }

            object IHttpResponse.OriginalResponse => OriginalResponse;

            public Task Close()
            {
                OriginalResponse.Close();
                IsClosed = true;
                return Task.CompletedTask;
            }
        }

        private bool FNeedToRemoveUrlReservation;
        private HttpListener FListener;
        private Task? FListenerThread;
        private readonly ManualResetEventSlim FTerminate;

        private void Listen()
        {
            Trace.WriteLine(TraceRes.SERVICE_STARTED);

            //
            // Ha a kiszolgalo ujrainditasra kerul akkor az FListenerThread erteke is megvaltozik
            //

            Task listenerThread = FListenerThread!;

            do
            {
                //
                // Using mivel hasznaljuk az AsyncWaitHandle property-t, lasd: https://devblogs.microsoft.com/pfxteam/do-i-need-to-dispose-of-tasks/
                //

                using Task<HttpListenerContext> getContext = FListener.GetContextAsync();

                if (WaitHandle.WaitAny(new WaitHandle[] { FTerminate.WaitHandle, ((IAsyncResult) getContext).AsyncWaitHandle }) is 0)
                    break;

                switch (getContext.Status)
                {
                    case TaskStatus.RanToCompletion:
                        _ = RaiseContextAvailableEvent(getContext.Result);
                        break;

                    case TaskStatus.Faulted:
                        Trace.WriteLine(string.Format(TraceRes.Culture, TraceRes.EXCEPTION_IN_LISTENER_THREAD, getContext.Exception));
                        break;
                }
            } while (true);

            Trace.WriteLine(TraceRes.SERVICE_TERMINATED);

            async Task RaiseContextAvailableEvent(HttpListenerContext context)
            {
                //
                // A feldolgozok CancellationToken-t kapnak ResetEvent helyett.
                //

                using CancellationTokenSource cancellation = new();

                Task processor = OnContextAvailable!
                (
                    new HttpSession
                    (
                        new HttpListenerRequestWrapper(context.Request),
                        new HttpListenerResponseWrapper(context.Response),
                        cancellation.Token
                    )
                );

                //
                // Ha a fo szall leall akkor megszakitjuk a feldolgozokat.
                //

                if (await Task.WhenAny(processor, listenerThread) == listenerThread)
                {
                    cancellation.Cancel();
                    await processor;
                }
            }
        }

        private static HttpListener CreateCore(string url) 
        {
            HttpListener result = new()
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

            ProcessStartInfo psi = new("netsh", arguments)
            {
                Verb            = "runas",
                CreateNoWindow  = true,
                WindowStyle     = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process netsh = Process.Start(psi);
            
            netsh.WaitForExit();
            if (netsh.ExitCode is not 0)
                #pragma warning disable CA2201 // Do not change the exception type to preserve backward compatibility
                throw new Exception(ErrorRes.NETSH_INVOCATION_FAILED);
                #pragma warning restore CA2201
        }
        #endregion

        #region Protected
        /// <inheritdoc/>
        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
            {
                if (FListenerThread?.IsCompleted is false)
                    Stop();

                FListener.Close();
                FTerminate.Dispose();
            }

            base.Dispose(disposeManaged);
        }
        #endregion

        #region Public
        /// <summary>
        /// Creates a new <see cref="HttpListenerWrapper"/> instance.
        /// </summary>
        public HttpListenerWrapper(string url)
        {
            FListener = CreateCore(url);
            FTerminate = new ManualResetEventSlim(false);
            Url = url;
        }

        /// <summary>
        /// Returns true if the Web Service is listening.
        /// </summary>
        public bool IsListening => FListener.IsListening && FListenerThread?.IsCompleted is false;

        /// <inheritdoc/>
        public string Url { get; }

        /// <summary>
        /// Returns true if the system should reserve the <see cref="Url"/>.
        /// </summary>
        public bool ReserveUrl { get; set; } = true;

        /// <inheritdoc/>
        public event Func<IHttpSession, Task> OnContextAvailable = _ => Task.CompletedTask;

        /// <summary>
        /// Starts the listener.
        /// </summary>
        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "There is no dead conditional code")]
        public void Start()
        {
            if (FTerminate.IsSet)
                FTerminate.Reset();

            again:
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

                FListener = CreateCore(Url);

                if (ReserveUrl && Environment.OSVersion.Platform is PlatformID.Win32NT && ex is HttpListenerException httpEx && httpEx.ErrorCode is 5 /*ERROR_ACCESS_DENIED*/ && !FNeedToRemoveUrlReservation)
                {
                    AddUrlReservation(Url);
                    FNeedToRemoveUrlReservation = true;

                    //
                    // Megprobaljuk ujra letrehozni.
                    //

                    goto again;
                }

                //
                // Ha nem URL lefoglalos gondunk volt akkor tovabb dobjuk a kivetelt
                //

                throw;
            }


            FListenerThread = Task.Factory.StartNew(Listen, TaskCreationOptions.LongRunning);
            FListenerThread.Start();
        }

        /// <summary>
        /// Stops the listener.
        /// </summary>
        public void Stop()
        {
            //
            // Nem fogadunk tobb kerest valamint jelezzuk a meg aktiv feldolgozoknak h alljanak le.
            //

            FTerminate.Set();
            FListenerThread!.Wait();
            FListener.Stop();

            if (FNeedToRemoveUrlReservation)
            {
                try
                {
                    RemoveUrlReservation(Url);
                }
                #pragma warning disable CA1031 // This method should not throw
                catch { }
                #pragma warning restore CA1031

                FNeedToRemoveUrlReservation = false;
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