/********************************************************************************
* ManualResetEventSlimExtensions.cs                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    internal static class ManualResetEventSlimExtensions
    {
        public static Task AsTask(this ManualResetEventSlim resetEvent) => AsTask(resetEvent, Timeout.Infinite);

        public static Task AsTask(this ManualResetEventSlim resetEvent, int timeoutMs)
        {
            TaskCompletionSource<object?> tcs = new();

            RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject
            (
                resetEvent.WaitHandle,
                (state, timedOut) =>
                {
                    if (timedOut)
                        tcs.SetCanceled();
                    else
                        tcs.SetResult(null);
                },
                state: null,
                timeoutMs,
                executeOnlyOnce: true
            );
            
            tcs
                .Task
                .ContinueWith(_ => registration.Unregister(null));
            
            return tcs.Task;
        }
    }
}
