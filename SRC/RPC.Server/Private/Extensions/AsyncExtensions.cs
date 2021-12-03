/********************************************************************************
* AsyncExtensions.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Primitives;

    internal static class AsyncExtensions
    {
        private static readonly MethodInfo FGenericJoin = ((Func<Func<Task>, Func<Task>, Task<object>>) Join<object>)
            .Method
            .GetGenericMethodDefinition();

        private static async Task<T> Join<T>(Func<Task> decorator, Func<Task> original)
        {
            await decorator();
            return await (Task<T>) original();
        }

        //
        // Ha kulonbozo visszateresu Task-okat akarunk osszefuzni akkor a TaskExtensions.Unwrap()
        // metodus nem jatszik
        //

        public static Task Before(Func<Task> original, Type returnType, Func<Task> decorator)
        {
            if (returnType == typeof(Task))
            {
                return InvokeAsync();

                async Task InvokeAsync()
                {
                    await decorator();
                    await original();
                }
            }

            if (typeof(Task).IsAssignableFrom(returnType)) // Task<>
            {
                Func<object?[], object> join = Cache.GetOrAdd(returnType, () => FGenericJoin
                    .MakeGenericMethod(returnType.GetGenericArguments().Single())
                    .ToStaticDelegate());

                return (Task) join(new object[] { decorator, original });
            }

            throw new NotSupportedException();
        }
    }
}
