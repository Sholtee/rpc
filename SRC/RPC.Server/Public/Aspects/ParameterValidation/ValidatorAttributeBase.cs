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
    using Primitives;
    using Interfaces.Properties;

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
            init
            {
                if (value is null)
                    throw new ArgumentNullException(nameof(value));

                ConstructorInfo? ctor = value.GetConstructor(Type.EmptyTypes);
                if (ctor is null)
                    throw new ArgumentException(Errors.PARAMETERLESS_CTOR_REQUIRED , nameof(value));

                ShouldRunImpl = ctor
                    .ToStaticDelegate()
                    .Invoke(Array.Empty<object?>()) as IConditionalValidatior ?? throw new ArgumentException
                    (
                        string.Format(Errors.Culture, Errors.NOT_ASSIGNABLE_FROM, value, typeof(IConditionalValidatior)),
                        nameof(value)
                    );
            }
        }

        /// <summary>
        /// See <see cref="ICustomValidator.SupportsNull"/>.
        /// </summary>
        public bool SupportsNull { get; }

        /// <inheritdoc/>
        public virtual bool ShouldRun(MethodInfo containingMethod, object?[] args) => ShouldRunImpl?.ShouldRun(containingMethod, args) ?? true;
    }
}
