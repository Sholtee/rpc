/********************************************************************************
* DefaultHandler.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc
{
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// The default request handler.
    /// </summary>
    public sealed class DefaultHandler : IRequestHandler
    {
        /// <inheritdoc/>
        public IRequestHandler? Next { get; }

        /// <inheritdoc/>
        public Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            //
            // Ha csak valamelyik Handler at nem allitotta akkor HTTP 200 lesz a visszateresi kod.
            //

            context.Response.Close();
            return Task.CompletedTask;
        }
    }
}