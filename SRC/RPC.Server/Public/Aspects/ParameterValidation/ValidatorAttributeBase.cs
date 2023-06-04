/********************************************************************************
* ValidatorAttributeBase.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;

    /// <summary>
    /// Base class of validator attributes
    /// </summary>
    [SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix")]
    public abstract class ValidatorAttributeBase: Attribute, ICustomValidator
    {
        /// <summary>
        /// Creates a new <see cref="ValidatorAttributeBase"/> instance.
        /// </summary>
        protected ValidatorAttributeBase(bool supportsNull) => SupportsNull = supportsNull;

        /// <summary>
        /// See <see cref="ICustomValidator.SupportsNull"/>.
        /// </summary>
        public bool SupportsNull { get; }
    }
}
