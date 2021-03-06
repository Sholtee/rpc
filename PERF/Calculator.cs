﻿/********************************************************************************
* Calculator.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace Solti.Utils.Rpc.Perf
{
    using static Consts;
    using Server.Sample.Interfaces;

    [MemoryDiagnoser]
    public class Calculator
    {
        private RpcClientFactory Factory { get; set; }

        [GlobalSetup]
        public void Setup() => Factory = new RpcClientFactory("http://localhost:1986/api/");

        [GlobalCleanup]
        public void Cleanup() => Factory?.Dispose();

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public async Task Add()
        {
            ICalculator calculator = await Factory.CreateClient<ICalculator>();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                calculator.Add(1, 1);
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public async Task AddAsync()
        {
            ICalculator calculator = await Factory.CreateClient<ICalculator>();

            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                await calculator.AddAsync(1, 1);
            }
        }
    }
}
