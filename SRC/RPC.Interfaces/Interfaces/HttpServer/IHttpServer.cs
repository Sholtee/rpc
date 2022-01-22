/********************************************************************************
* IHttpServer.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    using Primitives.Patterns;

    /// <summary>
    /// Describes an abstract HTTP server.
    /// </summary>
    public interface IHttpServer: IDisposableEx
    {
        /// <summary>
        /// Returns true if the server is started.
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// The URL on which the server listens.
        /// </summary>
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "HttpListener accepts non-standard URLs (like '+' or '*').")]
        string Url { get; }

        /// <summary>
        /// If the server is listening, this method waits until a new request is availbale. 
        /// </summary>
        /// <exception cref="OperationCanceledException">Either the server was stopped or a <paramref name="cancellation"/> was requested.</exception>
        Task<IHttpSession> WaitForSessionAsync(CancellationToken cancellation);

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <exception cref="InvalidOperationException">The server has already been started.</exception>
        void Start();

        /// <summary>
        /// Stops the server.
        /// </summary>
        /// <remarks>This method should not free resources so the already started sessions can gracefully terminate.</remarks>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "The naming won't confuse the users.")]
        void Stop();
    }
}
