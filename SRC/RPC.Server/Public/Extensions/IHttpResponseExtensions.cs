/********************************************************************************
* IHttpResponseExtensions.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc
{
    using Interfaces;

    /// <summary>
    /// Defines some extenions against the <see cref="IHttpResponse"/> interface. 
    /// </summary>
    public static class IHttpResponseExtensions
    {
        /// <summary>
        /// Writes the given <paramref name="responseString"/> to the <paramref name="response"/>.
        /// </summary>
        public async static Task WriteResponseString(this IHttpResponse response, string responseString, CancellationToken cancellation = default)
        {
            if (response is null)
                throw new ArgumentNullException(nameof(response));

            response.Headers["Content-Type"] = "text/html";

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            await response.Payload.WriteAsync
            (
#if NETSTANDARD2_1_OR_GREATER
                buffer.AsMemory(0, buffer.Length)
#else
                buffer, 0, buffer.Length
#endif
                , cancellation
            );
        }
    }
}
