/********************************************************************************
* RpcResponse.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    internal sealed class RpcResponse
    {
        public object? Result { get; init; }

        public ExceptionInfo? Exception { get; init; }
    }
}
