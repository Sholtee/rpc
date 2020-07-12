/********************************************************************************
* IRpcResonse.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc.Internals
{
    internal interface IRpcResonse 
    {
        public object? Result { get; }

        public ExceptionInfo? Exception { get; }
    }
}
