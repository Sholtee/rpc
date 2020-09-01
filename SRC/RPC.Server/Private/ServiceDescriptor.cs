/********************************************************************************
* ServiceDescriptor.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    internal sealed class ServiceDescriptor : IServiceDescriptor
    {
        private static ProcessModule MainModule { get; } = Process.GetCurrentProcess().MainModule;

        public Task<string> Name => Task.FromResult(MainModule.FileVersionInfo.ProductName);

        public Task<Version> Version => Task.FromResult((Version) MainModule.FileVersionInfo);
    }
}
