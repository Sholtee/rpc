/********************************************************************************
* Calculator.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace Solti.Utils.Rpc.Perf
{
    using static Consts;
    using DI;
    
    [MemoryDiagnoser]
    public class Calculator
    {
        public interface ICalculator 
        {
            int Add(int a, int b);
            Task<int> AddAsync(int a, int b);
        }

        public class CalculatorModule : ICalculator 
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public int Add(int a, int b) => a + b;
            [MethodImpl(MethodImplOptions.NoInlining)]
            public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
        }

        private RpcService Service { get; set; }

        private RpcClient<ICalculator> Client { get; set; }

        [GlobalSetup]
        public void Setup() 
        {
            const string host = "http://127.0.0.1:1986/api/";

            Service = new RpcService(new ServiceContainer());
            Service.Register<ICalculator, CalculatorModule>();
            Service.Start(host);

            Client = new RpcClient<ICalculator>(host);
        }

        [GlobalCleanup]
        public void Cleanup() 
        {
            Client?.Dispose();
            Service?.Container?.Dispose();
            Service?.Dispose();
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void Add()
        {
            ICalculator calculator = Client.Proxy;

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                calculator.Add(1, 1);
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void AddAsync()
        {
            ICalculator calculator = Client.Proxy;

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                calculator.AddAsync(1, 1).GetAwaiter().GetResult();
            }
        }
    }
}
