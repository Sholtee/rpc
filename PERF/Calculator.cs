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
        //static Calculator() => RpcClientFactory.PreserveProxyAssemblies = true;

        private RpcClientFactory Factory { get; set; }

        [GlobalSetup]
        public void Setup() => Factory = new RpcClientFactory("http://localhost:1986/api/");

        [GlobalCleanup]
        public void Cleanup() => Factory?.Dispose();

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void Add()
        {
            ICalculator calculator = Factory.CreateClient<ICalculator>();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                calculator.Add(1, 1);
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void AddAsync()
        {
            ICalculator calculator = Factory.CreateClient<ICalculator>();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                calculator.AddAsync(1, 1).GetAwaiter().GetResult();
            }
        }
    }
}
