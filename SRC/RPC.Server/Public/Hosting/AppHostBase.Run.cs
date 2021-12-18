/********************************************************************************
* AppHostBase.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Hosting
{
    using Properties;

    public partial class AppHostBase
    {
        /// <summary>
        /// Starts the host as a command line application
        /// </summary>
        /// <remarks>In most of cases you should not override this method.</remarks>
        public override void OnRun()
        {
            //
            // Ide csak akkor jutunk ha az app parancssori argumentum nelkul vt meghivva.
            //

            Console.Title = "Server";

            OnStart();

            //
            // Ne Console.WriteLine()-t hasznaljunk mert az elszall ha az output at lett iranyitva.
            //

            Console.Out.WriteLine(Trace.RUNNING);

            //
            // Blokkolodunk amig a kiszolgalo leallitasra nem kerul (CTRL+ C-vel).
            //

            Func<int> readKey = Console.IsInputRedirected
                ? Console.In.Read
                : () => Console.ReadKey(intercept: true).KeyChar;

            while (readKey() != '\x3') {}

            OnStop();
        }
    }
}
