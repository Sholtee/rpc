/********************************************************************************
* IParameterValidator.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;

    /// <summary>
    /// Defines an abstract parameter validator.
    /// </summary>
    public interface IParameterValidator: ICustomValidator
    {
        /// <summary>
        /// Defines the layout of the validator method.
        /// </summary>
        void Validate(ParameterInfo param, object? value, IInjector currentScope);
    }
}
