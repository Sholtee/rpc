/********************************************************************************
* RequestLimiterAspectAttribute.cs                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

#pragma warning disable CA1707 // Identifiers should not contain underscores

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Sets the request threshold on a given method.
    /// </summary>
    /// <remarks>The threshold limits the number of requests to be served in the direction of a remote endpoint.</remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RequestThresholdAttribute : Attribute
    {
        /// <summary>
        /// The value.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Creates a new a <see cref="RequestThresholdAttribute"/> instance.
        /// </summary>
        public RequestThresholdAttribute(int value) => Value = value;
    }
}
