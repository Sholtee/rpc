/********************************************************************************
* IConditionalValidatior.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Determines whether a validator should run or not.
    /// </summary>
    /// <remarks>Implementations should not contain complex logics, so this interface has no asynchronous counterpart.</remarks>
    public interface IConditionalValidatior
    {
        /// <summary>
        /// Returns true if the validator should run.
        /// </summary>
        bool ShouldRun(MethodInfo containingMethod, IInjector currentScope);
    }
}
