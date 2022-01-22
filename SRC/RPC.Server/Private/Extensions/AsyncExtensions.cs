/********************************************************************************
* AsyncExtensions.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Primitives;

    internal static class AsyncExtensions
    {
        private static readonly MethodInfo FGenericDecorate = ((Func<Func<Task>, Func<Func<Task>, Task<Task>>, Task<object>>) Decorate<object>)
            .Method
            .GetGenericMethodDefinition();

        private static async Task Decorate(Func<Task> original, Func<Func<Task>, Task<Task>> decorator)
        {
            Task task = await decorator(original);
            await task;
        }

        private static async Task<T> Decorate<T>(Func<Task> original, Func<Func<Task>, Task<Task>> decorator)
        {
            Task<T> task = (Task<T>) await decorator(original);
            return await task;
        }

        //
        // Ha kulonbozo visszateresu Task-okat akarunk osszefuzni akkor a TaskExtensions.Unwrap()
        // metodus nem jatszik
        //

        public static Task Decorate(Func<Task> original, Type returnType, Func<Func<Task>, Task<Task>> decorator)
        {
            if (returnType == typeof(Task))
                return Decorate(original, decorator);

            if (typeof(Task).IsAssignableFrom(returnType)) // Task<>
            {
                Func<object?[], object> decorate = Cache.GetOrAdd(returnType, () => FGenericDecorate
                    .MakeGenericMethod(returnType.GetGenericArguments().Single())
                    .ToStaticDelegate());

                return (Task) decorate(new object[] { original, decorator });
            }

            throw new NotSupportedException();
        }

        public static async Task<bool> WaitAsync(this Task task, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellation = new();

            if (await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellation.Token)) == task)
            {
                //
                // Leallitjuk a varakozast
                //

                timeoutCancellation.Cancel();
                return true;
            }

            return false;
        }
    }
}
