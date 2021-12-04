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
        private static readonly MethodInfo FGenericBefore = ((Func<Func<Task>, Func<Task>, Task<object>>) Before<object>)
            .Method
            .GetGenericMethodDefinition();

        private static async Task<T> Before<T>(Func<Task> decorator, Func<Task> original)
        {
            await decorator();
            return await (Task<T>) original();
        }

        private static async Task Before(Func<Task> decorator, Func<Task> original)
        {
            await decorator();
            await original();
        }

        //
        // Ha kulonbozo visszateresu Task-okat akarunk osszefuzni akkor a TaskExtensions.Unwrap()
        // metodus nem jatszik
        //

        public static Task Before(Func<Task> original, Type returnType, Func<Task> decorator)
        {
            if (returnType == typeof(Task))
                return Before(decorator, original);

            if (typeof(Task).IsAssignableFrom(returnType)) // Task<>
            {
                Func<object?[], object> join = Cache.GetOrAdd(returnType, () => FGenericBefore
                    .MakeGenericMethod(returnType.GetGenericArguments().Single())
                    .ToStaticDelegate());

                return (Task) join(new object[] { decorator, original });
            }

            throw new NotSupportedException();
        }
    }
}
