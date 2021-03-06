﻿/********************************************************************************
* TransactionalAttribute.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Data;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Marks a method to be transactional.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TransactionalAttribute : Attribute
    {
        /// <summary>
        /// The <see cref="System.Data.IsolationLevel"/> of the transaction.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.Unspecified;
    }
}
