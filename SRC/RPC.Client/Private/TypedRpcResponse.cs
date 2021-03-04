/********************************************************************************
* TypedRpcResponse.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    internal abstract class TypedRpcResponse<TResult> : IRpcResonse
    {
        public TResult? Result { get; set; }

        public ExceptionInfo? Exception { get; set; }

        object? IRpcResonse.Result => Result;
    }

    internal class ValueRpcResponse<TResult>: TypedRpcResponse<TResult?> where TResult: struct // Nem eleg ha csak az osben van Nullable megjeloles
    {
    }

    internal class ReferenceRpcResponse<TResult> : TypedRpcResponse<TResult?> where TResult : class
    {
    }
}
