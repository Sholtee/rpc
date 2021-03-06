﻿/********************************************************************************
* IParameterValidator.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

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
        void Validate(ParameterInfo param, object? value, IInjector currentScope);
    }

    /// <summary>
    /// Defines an abstract, asynchronous parameter validator.
    /// </summary>
    public interface IAsyncParameterValidator : IParameterValidator
    {
        /// <summary>
        /// Defines the asynchronous layout of the validator method.
        /// </summary>
        Task ValidateAsync(ParameterInfo param, object? value, IInjector currentScope);
    }
}
