/********************************************************************************
* Calculator.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Server.Sample
{
    internal class Calculator : ICalculator
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Add(int a, int b) => a + b;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
    }
}
