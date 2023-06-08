/********************************************************************************
* RoleValidatorAspectAttribute.cs                                               *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Aspects
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;

    /// <summary>
    /// Validates if the caller have sufficient privileges to call module methods.
    /// </summary>
    /// <remarks>In order to use this aspect you have to implement and register the <see cref="IRoleManager"/> service.</remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class RoleValidatorAspectAttribute : AspectAttribute
    {
        /// <summary>
        /// Creates a new <see cref="RoleValidatorAspectAttribute"/> instance.
        /// </summary>
        public RoleValidatorAspectAttribute() : base(typeof(RoleValidator));
    }
}
