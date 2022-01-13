/********************************************************************************
* IRequestHandler.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CA1716 // Identifiers should not match keywords

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Contains the context of a HTTP request.
    /// </summary>
    public class RequestContext
    {
        /// <summary>
        /// Creates a new <see cref="RequestContext"/> instance.
        /// </summary>
        public RequestContext() { }

        /// <summary>
        /// Creates a new <see cref="RequestContext"/> instance.
        /// </summary>
        public RequestContext(RequestContext original) 
        {
            if (original is null)
                throw new ArgumentNullException(nameof(original));

            Request = original.Request;
            Response = original.Response;
            Cancellation = original.Cancellation;
            Scope = original.Scope;
        }

        /// <summary>
        /// The HTTP request to read
        /// </summary>
        public HttpListenerRequest Request { get; init; }

        /// <summary>
        /// The HTTP response to write
        /// </summary>
        public HttpListenerResponse Response { get; init; }

        /// <summary>
        /// The cancellation token.
        /// </summary>
        public CancellationToken Cancellation { get; init; }

        /// <summary>
        /// The request scope.
        /// </summary>
        public IInjector Scope { get; init; }
    }

    /// <summary>
    /// Describes a request handler service.
    /// </summary>
    /// <remarks>Requests are processed in a pipeline which consits of one or more <see cref="IRequestHandler"/> service.</remarks>
    public interface IRequestHandler
    {
        /// <summary>
        /// The next handler to be called.
        /// </summary>
        /// <remarks>A handler should not invoke the <see cref="Next"/> instance after closing the session (calling the <see cref="HttpListenerResponse.Close()"/> method).</remarks>
        IRequestHandler? Next { get; }

        /// <summary>
        /// Does some handler specific work.
        /// </summary>
        /// <remarks></remarks>
        Task Handle(RequestContext context);
    }
}
