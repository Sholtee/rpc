/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Text.Json;

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
            int Add(int a, int b);
        }

        public class Module : IModule 
        {
            public int Add(int a, int b) => a + b;
        }

        private IServiceContainer Container { get; set; }

        private Internals.ModuleInvocation Invoke { get; set; }

        private IRequestContext Context { get; set; }

        [GlobalSetup]
        public void GlobalSetup() 
        {
            Container = new ServiceContainer();
            Container.Service<IModule, Module>(Lifetime.Scoped);

            var bldr = new ModuleInvocationBuilder();
            bldr.AddModule<IModule>();

            Invoke = new ModuleInvocationBuilder().Build();

            Context = new RequestContext(null, nameof(IModule), nameof(IModule.Add), JsonSerializer.Serialize(new object[] { 1, 1 }), null);
        }

        [GlobalCleanup]
        public void GlobalCleanup() => Container.Dispose();

        [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
        public void DirectInvocation()
        {
            using (IInjector injector = Container.CreateInjector())
            {
                for (int i = 0; i < OperationsPerInvoke; i++)
                {
                    int sum = injector.Get<IModule>().Add(1, 1);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void UsingTheBuiltDelegate() 
        {
            using (IInjector injector = Container.CreateInjector())
            {
                for (int i = 0; i < OperationsPerInvoke; i++)
                {
                    object sum = Invoke(injector, Context);
                }
            }
        }
    }
}
