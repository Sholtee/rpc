﻿/********************************************************************************
* ICalculator.cs                                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Server.Sample
{
    public interface ICalculator
    {
        int Add(int a, int b);
        Task<int> AddAsync(int a, int b);
    }
}