/********************************************************************************
* TransactionAspectAttribute.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;
    using Internals;

    /// <summary>
    /// Indicates that the methods of a service may use transactions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TransactionAspectAttribute : AspectAttribute
    {
        /// <summary>
        /// Creates a new <see cref="TransactionAspectAttribute"/> instance.
        /// </summary>
        public TransactionAspectAttribute() : base(typeof(TransactionManager)) { }
    }
}
