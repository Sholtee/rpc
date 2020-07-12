/********************************************************************************
* TypedRpcResponse.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc.Internals
{
    internal class TypedRpcResponse<TResult>: IRpcResonse
    {
        #pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public TResult Result { get; set; }
        #pragma warning restore CS8618

        public ExceptionInfo? Exception { get; set; }

        object? IRpcResonse.Result => Result;

        ExceptionInfo? IRpcResonse.Exception => Exception;
    }
}
