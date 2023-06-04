/********************************************************************************
* IPropertyValidator.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Defines an abstract property validator.
    /// </summary>
    public interface IPropertyValidator: ICustomValidator
    {
        /// <summary>
        /// The message that is thrown when the validation failed.
        /// </summary>
        string PropertyValidationErrorMessage { get; set; }

        /// <summary>
        /// Defines the layout of the validator method.
        /// </summary>
        void Validate(PropertyInfo prop, object? value);
    }
}
