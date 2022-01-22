/********************************************************************************
* HttpSession.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Servers
{
    using Interfaces;

    /// <summary>
    /// Default <see cref="IHttpSession"/> implementation.
    /// </summary>
    public sealed record HttpSession(IHttpServer Server, IHttpRequest Request, IHttpResponse Response) : IHttpSession
    {
    }
}