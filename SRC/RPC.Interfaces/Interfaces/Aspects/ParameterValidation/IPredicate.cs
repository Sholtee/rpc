/********************************************************************************
* IPredicate.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Describes a logic that tests for a condition.
    /// </summary>
    public interface IPredicate
    {
        /// <summary>
        /// Executes the predicate.
        /// </summary>
        bool Execute(object? value, IInjector currentScope);
    }

    /// <summary>
    /// Describes a logic that tests for a condition.
    /// </summary>
    public interface IAsyncPredicate: IPredicate
    {
        /// <summary>
        /// Executes the predicate.
        /// </summary>
        Task<bool> ExecuteAsync(object? value, IInjector currentScope);
    }
}
