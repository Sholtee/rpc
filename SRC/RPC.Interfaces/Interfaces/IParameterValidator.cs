/********************************************************************************
* IParameterValidator.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;

namespace Solti.Utils.Rpc.Aspects
{
    /// <summary>
    /// Defines an abstract parameter validator.
    /// </summary>
    public interface IParameterValidator
    {
        /// <summary>
        /// Defines the layout of the validator method.
        /// </summary>
        void Validate(ParameterInfo param, object? value);
    }
}
