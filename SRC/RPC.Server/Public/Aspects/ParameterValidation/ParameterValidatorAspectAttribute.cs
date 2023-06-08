/********************************************************************************
* ParameterValidatorAspectAttribute.cs                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;
    using Internals;

    /// <summary>
    /// Indicates that the methods of a service may validate their parameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ParameterValidatorAspectAttribute : AspectAttribute
    {
        /// <summary>
        /// Returns true if the validator should collect all the validation errors.
        /// </summary>
        public bool Aggregate { get; }

        /// <summary>
        /// Creates a new <see cref="ParameterValidatorAspectAttribute"/> instance.
        /// </summary>
        public ParameterValidatorAspectAttribute(bool aggregate = false): base(typeof(ParameterValidator)) => Aggregate = aggregate;
    }
}
