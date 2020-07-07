/********************************************************************************
* TypedRpcResponse.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc
{
    internal interface IRpcResonse 
    {
        public object? Result { get; }

        public ExceptionInfo? Exception { get; }
    }

    /// <summary>
    /// Describes a typed RPC response
    /// </summary>
    public class TypedRpcResponse<TResult>: IRpcResonse
    {
        /// <summary>
        /// The result (if the remote method call completed successfully).
        /// </summary>
        #pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public TResult Result { get; set; }
        #pragma warning restore CS8618

        /// <summary>
        /// The exception (if something went wrong).
        /// </summary>
        public ExceptionInfo? Exception { get; set; }

        object? IRpcResonse.Result => Result;

        ExceptionInfo? IRpcResonse.Exception => Exception;
    }
}
