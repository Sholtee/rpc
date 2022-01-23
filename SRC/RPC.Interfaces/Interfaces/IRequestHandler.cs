/********************************************************************************
* IRequestHandler.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Describes a request handler service.
    /// </summary>
    /// <remarks>Requests are processed in a pipeline which consits of one or more <see cref="IRequestHandler"/> service.</remarks>
    public interface IRequestHandler
    {
        /// <summary>
        /// The next handler to be called.
        /// </summary>
        /// <remarks>A handler should not invoke the <see cref="Next"/> instance after closing the session.</remarks>
        IRequestHandler? Next { get; }

        /// <summary>
        /// Does some handler specific work.
        /// </summary>
        Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation);
    }
}
