/********************************************************************************
* ValidatorAttributeBase.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Solti.Utils.Rpc.Interfaces
{
    using DI.Interfaces;
    using Primitives;
    using Properties;

    /// <summary>
    /// Base class for validator attributes e.g.: <see cref="NotNullAttribute"/>.
    /// </summary>
    [SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix")]
    public abstract class ValidatorAttributeBase: Attribute, IConditionalValidatior, ICustomValidator
    {
        private IConditionalValidatior? ShouldRunImpl;

        /// <summary>
        /// Creates a new <see cref="ValidatorAttributeBase"/> instance.
        /// </summary>
        protected ValidatorAttributeBase(bool supportsNull) => SupportsNull = supportsNull;

        /// <summary>
        /// If set, it should point to a class implementing the <see cref="IConditionalValidatior"/> interface.
        /// </summary>
        public Type? Condition 
        { 
            get => ShouldRunImpl?.GetType();
            set
            {
                if (value is null) ShouldRunImpl = null;
                else
                {
                    ConstructorInfo? ctor = value.GetConstructor(Type.EmptyTypes);
                    if (ctor is null)
                        throw new ArgumentException(Errors.PARAMETERLESS_CTOR_REQUIRED , nameof(value));

                    ShouldRunImpl = (IConditionalValidatior?) ctor.ToStaticDelegate().Invoke(Array.Empty<object?>());
                }
            }
        }

        /// <summary>
        /// See <see cref="ICustomValidator.SupportsNull"/>.
        /// </summary>
        public bool SupportsNull { get; }

        /// <summary>
        /// See <see cref="IConditionalValidatior.ShouldRun(MethodInfo, IInjector)"/>.
        /// </summary>
        public virtual bool ShouldRun(MethodInfo containingMethod, IInjector currentScope) => ShouldRunImpl?.ShouldRun(containingMethod, currentScope) ?? true;
    }
}
