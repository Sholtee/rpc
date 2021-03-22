/********************************************************************************
* Calculator.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Server.Sample
{
    using Interfaces;
    using Rpc.Interfaces;

    internal class Calculator : ICalculator
    {
        private readonly IRequestContext FContext;
        public Calculator(IRequestContext context) => FContext = context ?? throw new ArgumentNullException(nameof(context));
        public int Add(int a, int b) => a + b;
        public Task<int> AddAsync(int a, int b)
        {
            FContext.Cancellation.ThrowIfCancellationRequested();
            return Task.FromResult(a + b);
        }
        public int ParseInt(string val) => int.Parse(val);

        public void TimeConsumingOperation() => Thread.Sleep(3000);

        public double PI => Math.PI;
    }
}
