/********************************************************************************
* Calculator.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using BenchmarkDotNet.Attributes;

namespace Solti.Utils.Rpc.Perf
{
    using DI;

    [MemoryDiagnoser]
    public class Calculator
    {
        public interface ICalculator 
        {
            int Add(int a, int b);
        }

        public class CalculatorModule : ICalculator 
        {
            public int Add(int a, int b) => a + b;
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

        const int OperationsPerInvoke = 50;

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void Add()
        {
            ICalculator calculator = Client.Proxy;

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                calculator.Add(1, 1);
            }
        }
    }
}
