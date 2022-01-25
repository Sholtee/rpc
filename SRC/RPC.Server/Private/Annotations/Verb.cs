/********************************************************************************
* VerbAttribute.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc.Internals
{
    /// <summary>
    /// Describes the verb that triggers the annotated method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class VerbAttribute: Attribute // ennek nem kell az Interfaces projektben lennie (nem a klienssel kozos osztaly)
    {
        /// <summary>
        /// The verb(s).
        /// </summary>
        public IReadOnlyList<string> Verbs { get; }

        /// <summary>
        /// Creates a new <see cref="VerbAttribute"/> instance.
        /// </summary>
        public VerbAttribute(params string[] verbs) => Verbs = verbs ?? throw new ArgumentNullException(nameof(verbs));

        /// <summary>
        /// Creates a new <see cref="VerbAttribute"/> instance.
        /// </summary>
        [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments")]
        public VerbAttribute(string verb) : this(new[] { verb ?? throw new ArgumentNullException(nameof(verb)) }) { }
    }
}
