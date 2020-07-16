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

        public override bool ShouldUse() => true;

        public override void Start() 
        {
            Console.Error.WriteLine(Resources.NO_HOSTING);
            Environment.Exit(-1);
        }

        public override void Stop()
        {
        }
    }
}
