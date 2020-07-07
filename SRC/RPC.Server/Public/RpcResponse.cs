/********************************************************************************
* RpcResponse.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// Describes the response to be serialized and sent to the client.
    /// </summary>
    public class RpcResponse
    {
        /// <summary>
        /// The result (if the remote method call completed successfully).
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// The exception (if something went wrong).
        /// </summary>
        public ExceptionInfo? Exception { get; set; }
    }
}
