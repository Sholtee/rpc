/********************************************************************************
* AppHost.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;
using System.IO;

namespace Solti.Utils.Rpc.Server.Sample
{
    using Hosting;

    public class AppHost : AppHostBase
    {
        public override string Name => "Calculator";

        public override string Url => "http://127.0.0.1:1986/api/";

        public override void OnRegisterModules(IModuleRegistry registry)
        {
            base.OnRegisterModules(registry);
            registry.Register<ICalculator, Calculator>();
        }

        public StreamWriter Log { get; } = new StreamWriter
        (
            Path.Combine
            (
                Path.GetDirectoryName(typeof(AppHost).Assembly.Location)!, 
                $"log-{Process.GetCurrentProcess().Id}.txt"
            )
        );

        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
                Log.Dispose();
            base.Dispose(disposeManaged);
        }

        public override void OnStart()
        {
            base.OnStart();
            Log.WriteLine($"Host started. Runner type is: {Runner.GetType().Name}");
        }

        public override void OnUnhandledException(Exception ex) => Log.Write(ex.Message);
    }
}
