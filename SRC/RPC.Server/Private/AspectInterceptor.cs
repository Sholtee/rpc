/********************************************************************************
* AspectInterceptor.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Proxy;

    /// <summary>
    /// Defines the base interceptor of aspects.
    /// </summary>
    public abstract class AspectInterceptor<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
    {
        /// <summary>
        /// Creates a new <see cref="AspectInterceptor{TInterface}"/> instance.
        /// </summary>
        protected AspectInterceptor(TInterface target) : base(target ?? throw new ArgumentNullException(nameof(target)))
        {
        }

        /// <summary>
        /// The decorator function.
        /// </summary>
        protected virtual object? Decorator(InvocationContext context, Func<object?> callNext)
        {
            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            return callNext();
        }

        /// <summary>
        /// The decorator function.
        /// </summary>
        protected virtual async Task<Task> DecoratorAsync(InvocationContext context, Func<Task> callNext)
        {
            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            Task t = callNext();
            await t;
            return t;
        }

        /// <summary>
        /// Aspect specific logic.
        /// </summary>
        public override object? Invoke(InvocationContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (typeof(Task).IsAssignableFrom(context.Method.ReturnType)) // Task | Task<>
            {
                return AsyncExtensions.Decorate
                (
                    () => (Task) base.Invoke(context)!,
                    context.Method.ReturnType,
                    original => DecoratorAsync(context, original)
                );
            }

            return Decorator(context, () => base.Invoke(context));
        }
    }
}
