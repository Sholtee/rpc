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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

#pragma warning disable CA1054 // URI-like parameters should not be strings
#pragma warning disable CA1056 // URI-like properties should not be strings
#pragma warning disable CA1031 // We have to catch all kind of exceptions here
#pragma warning disable CA1508 // Avoid dead conditional code

namespace Solti.Utils.Rpc
{
    using DI.Interfaces;
    using Interfaces;
    using Primitives.Patterns;
    using Primitives.Threading;
    using Properties;

    /// <summary>
    /// Implements a general Web Service over HTTP. 
    /// </summary>
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
                    [nameof(Url)] = Url
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

                            injector
                                .Get<IRequestHandler>()
                                .HandleAsync(new RequestContext 
                                {
                                    Request = getContext.Result.Request,
                                    Response = getContext.Result.Response,
                                    Cancellation = FListenerCancellation.Token,
                                    Scope = injector
                                })
                                .ContinueWith(_ => Interlocked.Decrement(ref FActiveRequests), TaskContinuationOptions.ExecuteSynchronously);

                            break;
                        case TaskStatus.Faulted:
                            logger?.LogError(getContext.Exception.ToString());
                            break;
                    }
                } while (true);

                logger?.LogInformation(Trace.SERVICE_TERMINATED);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(string.Format(Trace.Culture, Trace.EXCEPTION_IN_LISTENER_THREAD, ex));
            }
        }

        private HttpListener CreateCore() 
        {
            HttpListener result = new()
            {
                IgnoreWriteExceptions = true
            };

            try
            {
                result.Prefixes.Add(Url);
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
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            Process netsh = System.Diagnostics.Process.Start(psi);
            
            netsh.WaitForExit();
            if (netsh.ExitCode is not 0)
                #pragma warning disable CA2201 // Do not change the exception type to preserve backward compatibility
                throw new Exception(Errors.NETSH_INVOCATION_FAILED);
                #pragma warning restore CA2201
        }
        #endregion

        #region Protected
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
        #endregion

        #region Public
        /// <summary>
        /// Creates a new <see cref="WebService"/> instance.
        /// </summary>
        public WebService(string url, IServiceCollection serviceCollection)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            ScopeFactory = DI.ScopeFactory.Create(serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection)));
        }

        /// <summary>
        /// Returns true if the Web Service has already been started (which does not imply that it <see cref="IsListening"/>).
        /// </summary>
        public bool IsStarted => FListenerThread is not null;

        /// <summary>
        /// Returns true if the Web Service is listening.
        /// </summary>
        public bool IsListening => FListener?.IsListening is true && FListenerThread?.IsAlive is true;

        /// <summary>
        /// Returns the <see cref="IScopeFactory"/> related to this instance.
        /// </summary>
        public IScopeFactory ScopeFactory { get; }

        /// <summary>
        /// The URL on which the server listens,
        /// </summary>
        public string Url { get; }

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
                FListener = CreateCore();

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

                    if (Environment.OSVersion.Platform is PlatformID.Win32NT && ex is HttpListenerException httpEx && httpEx.ErrorCode is 5 /*ERROR_ACCESS_DENIED*/ && !FNeedToRemoveUrlReservation)
                    {
                        AddUrlReservation(Url);
                        FNeedToRemoveUrlReservation = true;

                        //
                        // Megprobaljuk ujra letrehozni.
                        //

                        goto createcore;
                    }

                    //
                    // Ha nem URL lefoglalos gondunk volt akkor tovabb dobjuk a kivetelt
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
        public void Stop() => Stop(TimeSpan.FromMinutes(2));

        /// <summary>
        /// Shuts down the Web Service.
        /// </summary>
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
                // Ha a SpinWait-bol timeout-tal jottunk ki akkor meg lehet feldolgozo aki hivatkozza a megszakitast
                // (viszont feldolgozoban keletkezett kivetel nem szopathatja be a kiszolgalot)
                //

                FListenerCancellation.Dispose();
                FListenerCancellation = null;

                if (FNeedToRemoveUrlReservation)
                {
                    try
                    {
                        RemoveUrlReservation(Url.ToString());
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