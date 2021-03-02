/********************************************************************************
* IPropertyValidator.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

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
        void Validate(PropertyInfo prop, object? value, IInjector currentScope);
    }

    /// <summary>
    /// Defines an abstract, asynchronous property validator.
    /// </summary>
    public interface IAsyncPropertyValidator : IPropertyValidator
    {
        /// <summary>
        /// Defines the asynchronous layout of the validator method.
        /// </summary>
        Task ValidateAsync(PropertyInfo prop, object? value, IInjector currentScope);
    }
}
