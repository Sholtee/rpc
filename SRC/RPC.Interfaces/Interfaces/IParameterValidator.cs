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
    public interface IParameterValidator
    {
        /// <summary>
        /// If set, it should point to a class implementing the <see cref="IConditionalValidatior"/> interface.
        /// </summary>
        Type? Condition { get; set; }

        /// <summary>
        /// Defines the layout of the validator method.
        /// </summary>
        void Validate(ParameterInfo param, object? value, IInjector currentScope);
    }
}
