/********************************************************************************
* IParameterValidator.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Defines an abstract parameter validator.
    /// </summary>
    public interface IParameterValidator: ICustomValidator
    {
        /// <summary>
        /// The message that is thrown when the validation failed.
        /// </summary>
        string ParameterValidationErrorMessage { get; set; }

        /// <summary>
        /// Defines the layout of the validator method.
        /// </summary>
        void Validate(ParameterInfo param, object? value);
    }
}
