/********************************************************************************
* DefaultHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    using Interfaces;
    using Properties;

    internal class DefaultHostRunner: HostRunner
    {
        internal DefaultHostRunner(IHost host) : base(host) { }

        public override void Start() => throw new Exception(Errors.NO_HOSTING);

        public override void Stop()
        {
        }

        #region Factory
        private sealed class FactoryImpl : IHostRunnerFactory
        {
            public bool ShouldUse => true;

            public IHostRunner CreateRunner(IHost host) => new DefaultHostRunner(host);
        }

        public static IHostRunnerFactory Factory { get; } = new FactoryImpl();
        #endregion
    }
}
