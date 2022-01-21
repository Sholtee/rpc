/********************************************************************************
* HttpSession.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading;

namespace Solti.Utils.Rpc.Servers
{
    using Interfaces;

    /// <summary>
    /// Default <see cref="IHttpSession"/> implementation.
    /// </summary>
    public sealed record HttpSession(IHttpRequest Request, IHttpResponse Response, in CancellationToken Cancellation) : IHttpSession
    {
    }
}