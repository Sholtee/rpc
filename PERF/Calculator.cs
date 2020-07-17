/********************************************************************************
* Calculator.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using BenchmarkDotNet.Attributes;

namespace Solti.Utils.Rpc.Perf
{
    using static Consts;
    using Server.Sample;

    [MemoryDiagnoser]
    public class Calculator
    {
        private RpcClient<ICalculator> Client { get; set; }

        [GlobalSetup]
        public void Setup() => Client = new RpcClient<ICalculator>("http://127.0.0.1:1986/api/");

        [GlobalCleanup]
        public void Cleanup() => Client?.Dispose();

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
