/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.IO;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace Solti.Utils.Rpc.Perf
{
    using static Consts;

    using DI;
    using DI.Interfaces;

    using Internals;

    [MemoryDiagnoser]
    public class ModuleInvocation
    {
        public interface IModule 
        {
            void Foo();
        }

        public class Module : IModule 
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Foo() { }
        }

        private IServiceContainer Container { get; set; }

        private static Stream Payload { get; } = CreatePayload();

        private static Stream CreatePayload() 
        {
            Stream result = new MemoryStream();
            
            var sw = new StreamWriter(result); // ne szabaditsuk fel mert felszabaditja az eredmenyt is

            sw.Write("[]");
            sw.Flush();

            result.Seek(0, SeekOrigin.Begin);

            return result;
        }

        private Internals.ModuleInvocation Invoke { get; set; }

        private IRequestContext Context { get; set; }

        [GlobalSetup]
        public void GlobalSetup() 
        {
            Container = new ServiceContainer();
            Container.Service<IModule, Module>(Lifetime.Scoped);

            var bldr = new ModuleInvocationBuilder();
            bldr.AddModule<IModule>();
            Invoke = bldr.Build();

            Context = new RequestContext(null, nameof(IModule), nameof(IModule.Foo), Payload, null, default);
        }

        [GlobalCleanup]
        public void GlobalCleanup() => Container.Dispose();

        [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
        public void DirectInvocation()
        {
            using IInjector injector = Container.CreateInjector();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                injector.Get<IModule>().Foo();
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void UsingTheBuiltDelegate() 
        {
            using IInjector injector = Container.CreateInjector();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                Invoke(injector, Context).GetAwaiter().GetResult();
            }
        }
    }
}
