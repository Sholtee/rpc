/********************************************************************************
* ServiceDescriptor.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;

namespace Solti.Utils.Rpc.Internals
{
    using Interfaces;

    internal sealed class ServiceDescriptor : IServiceDescriptor
    {
        private static ProcessModule MainModule { get; } = Process.GetCurrentProcess().MainModule;

        public string Name => MainModule.FileVersionInfo.ProductName;

        public Version Version => MainModule.FileVersionInfo;
    }
}
