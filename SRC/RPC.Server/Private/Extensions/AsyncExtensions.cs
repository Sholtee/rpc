/********************************************************************************
* AsyncExtensions.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Primitives;

    internal static class AsyncExtensions
    {
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
                Func<Task<object>> invokeAsync = InvokeAsync<object>;

                return (Task) invokeAsync
                    .Method
                    .GetGenericMethodDefinition()
                    .MakeGenericMethod(returnType.GetGenericArguments().Single())
                    .ToInstanceDelegate()
                    .Invoke(invokeAsync.Target, Array.Empty<object?>());

                async Task<T> InvokeAsync<T>()
                {
                    await decorator();
                    return await (Task<T>) original();
                }
            }

            throw new NotSupportedException();
        }
    }
}
