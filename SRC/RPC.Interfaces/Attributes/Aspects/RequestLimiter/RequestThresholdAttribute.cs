/********************************************************************************
* RequestLimiterAspectAttribute.cs                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

#pragma warning disable CA1019 // Define accessors for attribute arguments

namespace Solti.Utils.Rpc.Interfaces
{
    using Properties;

    /// <summary>
    /// Sets the request threshold on a given method.
    /// </summary>
    /// <remarks>The threshold limits the number of requests to be served in the direction of a remote endpoint.</remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequestThresholdAttribute : Attribute
    {
        private readonly object FValue;

        /// <summary>
        /// The value.
        /// </summary>
        public int Value => FValue switch
        {
            int value => value,
            string variableName => int.TryParse(Environment.GetEnvironmentVariable(variableName), out int result)
                ? result
                : throw new InvalidOperationException(Errors.INVALID_VARIABLE),
            _ => throw new NotSupportedException()
        };

        /// <summary>
        /// Creates a new a <see cref="RequestThresholdAttribute"/> instance against a given <paramref name="value"/>.
        /// </summary>
        public RequestThresholdAttribute(int value) => FValue = value;

        /// <summary>
        /// Creates a new a <see cref="RequestThresholdAttribute"/> instance against a given <paramref name="variableName"/>.
        /// </summary>
        /// <remarks>Use this constructor if you want to change the threshold value in runtime.</remarks>
        public RequestThresholdAttribute(string variableName) => FValue = variableName ?? throw new ArgumentNullException(nameof(variableName));
    }
}
