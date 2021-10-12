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

    using DI.Interfaces;

    using Internals;
    using Interfaces;

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

        private IScopeFactory ScopeFactory { get; set; }

        private IRequestContext Context { get; set; }

        [GlobalSetup]
        public void GlobalSetup() 
        {
            var bldr = new ModuleInvocationBuilder();
            bldr.AddModule<IModule>();
            Invoke = bldr.Build();

            ScopeFactory = DI.ScopeFactory.Create(svcs => svcs.Service<IModule, Module>(Lifetime.Scoped));

            Context = new RequestContext(null, nameof(IModule), nameof(IModule.Foo), Payload, default);
        }

        [GlobalCleanup]
        public void GlobalCleanup() => ScopeFactory?.Dispose();

        [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
        public void DirectInvocation()
        {
            using IInjector injector = ScopeFactory.CreateScope();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                injector.Get<IModule>().Foo();
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void UsingTheBuiltDelegate() 
        {
            using IInjector injector = ScopeFactory.CreateScope();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                Invoke(injector, Context).GetAwaiter().GetResult();
            }
        }
    }
}
