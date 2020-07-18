/********************************************************************************
* Calculator.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Server.Sample
{
    internal class Calculator : ICalculator
    {
        public int Add(int a, int b) => a + b;
        public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
    }
}
