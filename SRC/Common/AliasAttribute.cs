/********************************************************************************
* AliasAttribute.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// Adds a new alias to a member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
    public sealed class AliasAttribute: Attribute // TODO: move to a common interfaces project
    {
        /// <summary>
        /// The new name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates a new <see cref="AliasAttribute"/> instance.
        /// </summary>
        public AliasAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
