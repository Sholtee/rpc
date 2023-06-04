/********************************************************************************
* AsyncExtensions.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    internal static class AsyncExtensions
    {
        public static async Task<bool> WaitAsync(this Task task, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellation = new();

            if (await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellation.Token)) == task)
            {
                timeoutCancellation.Cancel();
                return true;
            }

            return false;
        }
    }
}
