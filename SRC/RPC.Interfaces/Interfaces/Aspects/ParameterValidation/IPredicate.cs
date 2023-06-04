/********************************************************************************
* IPredicate.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes a logic that tests for a condition.
    /// </summary>
    public interface IPredicate
    {
        /// <summary>
        /// Executes the predicate.
        /// </summary>
        bool Execute(object? value);
    }
}
