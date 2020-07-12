/********************************************************************************
* RpcResponse.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc.Internals
{
    internal sealed class RpcResponse
    {
        public object? Result { get; set; }

        public ExceptionInfo? Exception { get; set; }
    }
}
