/********************************************************************************
* TypedRpcResponse.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    internal class ValueRpcResponse<TResult>: IRpcResonse where TResult: struct
    {
        public TResult? Result { get; set; }

        public ExceptionInfo? Exception { get; set; }

        object? IRpcResonse.Result => Result;

        ExceptionInfo? IRpcResonse.Exception => Exception;
    }

    internal class ReferenceRpcResponse<TResult> : IRpcResonse where TResult : class
    {
        public TResult? Result { get; set; }

        public ExceptionInfo? Exception { get; set; }

        object? IRpcResonse.Result => Result;

        ExceptionInfo? IRpcResonse.Exception => Exception;
    }
}
