﻿/********************************************************************************
* ICalculator.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Server.Sample.Interfaces
{
    using Rpc.Interfaces;

    [PublishSchema]
    [ModuleLoggerAspect, ParameterValidatorAspect]
    public interface ICalculator
    {
        int Add(int a, int b);
        Task<int> AddAsync(int a, int b);
        double PI { get; }
        int ParseInt([NotNull] string val);
        void TimeConsumingOperation();
    }
}
