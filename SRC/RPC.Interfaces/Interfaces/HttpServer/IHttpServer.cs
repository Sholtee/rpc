/********************************************************************************
* IHttpServer.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes an abstract HTTP server.
    /// </summary>
    public interface IHttpServer: IDisposable
    {
        /// <summary>
        /// Returns true if the server is listening.
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// The URL on which the server listens.
        /// </summary>
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "HttpListener accepts non-standard URLs (like '+' or '*').")]
        string Url { get; }

        /// <summary>
        /// Callback, invoked when a reuqest is available.
        /// </summary>
        /// <remarks>This callback should start a new <see cref="Task"/> for each sessions thus it won't block the listener thread.</remarks>
        [SuppressMessage("Design", "CA1003:Use generic event handler instances")]
        event Func<IHttpSession, Task> OnContextAvailable;

        /// <summary>
        /// Starts the listener thread.
        /// </summary>
        /// <exception cref="InvalidOperationException">The server has already been started.</exception>
        void Start();

        /// <summary>
        /// Stops the listener thread.
        /// </summary>
        /// <remarks>This method should not free resources so the already started sessions can gracefully terminate.</remarks>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "The naming won't confuse the users.")]
        void Stop();
    }
}
