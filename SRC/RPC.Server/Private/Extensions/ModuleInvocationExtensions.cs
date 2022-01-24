/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Internals
{
    /// <summary>
    /// Defines some extensions to the <see cref="ModuleInvocation"/> delegate.
    /// </summary>
    public static class ModuleInvocationExtensions // "delegate"-bol nem szarmazhatunk ezert ez a megoldas
    {
        internal static IDictionary<ModuleInvocation, IReadOnlyList<Type>> RelatedModules { get; } = new ConcurrentDictionary<ModuleInvocation, IReadOnlyList<Type>>();

        /// <summary>
        /// Gets the registered modules related to this <see cref="ModuleInvocation"/> instance.
        /// </summary>
        public static IReadOnlyList<Type> GetRelatedModules(this ModuleInvocation src) => RelatedModules[src ?? throw new ArgumentNullException(nameof(src))];
    }
}