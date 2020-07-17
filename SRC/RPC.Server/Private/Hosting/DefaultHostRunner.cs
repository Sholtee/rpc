/********************************************************************************
* DefaultHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    using Properties;

    internal class DefaultHostRunner: HostRunner
    {
        public DefaultHostRunner(IHost host) : base(host) { }

        public override bool ShouldUse => true;

        public override void Start() => throw new Exception(Resources.NO_HOSTING);

        public override void Stop()
        {
        }
    }
}
