/********************************************************************************
* AspectInterceptor.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;
    using Primitives;

    /// <summary>
    /// Defines the base interceptor of aspects.
    /// </summary>
    public abstract class AspectInterceptor : IInterfaceInterceptor
    {
        private static async Task InvokeAsync(AspectInterceptor self, IInvocationContext context, Next<IInvocationContext, object?> callNext)
        {
            await self.DecoratorAsync(context, async context =>
            {
                await (Task) callNext(context)!;
                return null;
            });
        }

        private static async Task<T> InvokeAsync<T>(AspectInterceptor self, IInvocationContext context, Next<IInvocationContext, object?> callNext) =>
            (T) (await self.DecoratorAsync(context, async context => await (Task<T>) callNext(context)!))!;

        private static StaticMethod MakeGenericInvoke(Type returnType) => FInvokeAsync
            .MakeGenericMethod
            (
                returnType
                    .GetGenericArguments()
                    .Single()
            )
            .ToStaticDelegate();

        private static readonly ConcurrentDictionary<Type, StaticMethod> FDelegateChache = new();

        #pragma warning disable CS4014
        private static readonly MethodInfo FInvokeAsync = ((MethodCallExpression) ((Expression<Action>) (() => InvokeAsync<object>(null!, null!, null!))).Body)
            .Method
            .GetGenericMethodDefinition();
        #pragma warning restore CS4014

        /// <summary>
        /// The decorator function.
        /// </summary>
        protected virtual object? Decorator(IInvocationContext context, Next<IInvocationContext, object?> callNext) => callNext(context);

        /// <summary>
        /// The async decorator function.
        /// </summary>
        protected virtual Task<object?> DecoratorAsync(IInvocationContext context, Next<IInvocationContext, Task<object?>> callNext) => callNext(context);

        /// <summary>
        /// Aspect specific logic.
        /// </summary>
        object? IInterfaceInterceptor.Invoke(IInvocationContext context, Next<IInvocationContext, object?> callNext)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            Type returnType = context.TargetMethod.ReturnType;

            if (returnType == typeof(Task))
            {
                return InvokeAsync(this, context, callNext);
            }

            if (typeof(Task).IsAssignableFrom(returnType)) // Task<>
            {
                StaticMethod ivokeAsync = FDelegateChache.GetOrAdd
                (
                    returnType,
                    MakeGenericInvoke
                );

                return ivokeAsync(this, context, callNext);
            }

            return Decorator(context, callNext);
        }
    }
}
