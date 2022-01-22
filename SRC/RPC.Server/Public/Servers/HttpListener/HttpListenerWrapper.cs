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

    using ErrorRes = Properties.Errors;

    /// <summary>
    /// Wraps the built-in <see cref="HttpListener"/> class to be used as a <see cref="IHttpServer"/> service. 
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
                FListener.Close();

            base.Dispose(disposeManaged);
        }
        #endregion

        #region Public
        /// <summary>
        /// Creates a new <see cref="HttpListenerWrapper"/> instance.
        /// </summary>
        public HttpListenerWrapper(string url) => FListener = CreateCore(Url = url);

        /// <inheritdoc/>
        public bool IsStarted => FListener.IsListening;

        /// <inheritdoc/>
        public string Url { get; }

        /// <summary>
        /// Returns true if the system should reserve the <see cref="Url"/>.
        /// </summary>
        public bool ReserveUrl { get; set; } = true;

        /// <inheritdoc/>
        public async Task<IHttpSession> WaitForSessionAsync(CancellationToken cancellation)
        {
            TaskCompletionSource<HttpListenerContext> cancelSignal = new();
            cancellation.Register(cancelSignal.SetCanceled);

            Task<HttpListenerContext> completed = await Task.WhenAny(FListener.GetContextAsync(), cancelSignal.Task);

            HttpListenerContext context;
            try
            {
                context = await completed;
            }

            //
            // A kiszolgalo leallitasra kerult: https://docs.microsoft.com/en-us/dotnet/api/system.net.httplistener.begingetcontext?view=net-6.0#exceptions
            //

            catch (HttpListenerException) { throw new OperationCanceledException(); }

            return new HttpSession
            (
                this,
                new HttpListenerRequestWrapper(context.Request),
                new HttpListenerResponseWrapper(context.Response)
            );        
        }

        /// <inheritdoc/>
        [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "There is no dead conditional code")]
        public void Start()
        {
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
        }

        /// <inheritdoc/>
        public void Stop()
        {
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