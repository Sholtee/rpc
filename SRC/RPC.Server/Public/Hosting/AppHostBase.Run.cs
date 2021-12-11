/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Hosting
{
    using Properties;

    public partial class AppHostBase
    {
        /// <summary>
        /// Starts the host as a command line application
        /// </summary>
        public override void OnRun()
        {
            //
            // Ide csak akkor jutunk ha az app parancssori argumentum nelkul vt meghivva.
            //

            Console.Title = "Server";

            OnStart();

            using CancellationTokenSource cancellationSrc = new();

            CancellationToken cancellation = cancellationSrc.Token;

            Console.CancelKeyPress += (_, e) =>
            {
                cancellationSrc.Cancel();

                //
                // E nelkul parhuzamosan ket modon probalnank leallitani az app-ot
                //

                e.Cancel = true;
            };

            //
            // A Console.ReadKey()-t nem lehet maskepp megszakitani mint ugy hogy kulon szalban hivjuk amit
            // ki tudunk alola utni.
            //

            Task task = Task.Factory.StartNew(() =>
            {
                Console.WriteLine(Trace.RUNNING);
                while (true)
                    //
                    // Kell ahhoz hogy a Console.CancelKeyPress mukodjon
                    //

                    Console.ReadKey(intercept: true);
            }, cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            //
            // Blokkolodunk amig a kiszolgalo leallitasra nem kerul
            //

            WaitHandle.WaitAny(new WaitHandle[]
            {
                cancellation.WaitHandle,
                ((IAsyncResult) task).AsyncWaitHandle
            });

            OnStop();
        }
    }
}
