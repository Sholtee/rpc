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
        public override bool ShouldUse() => true;
        public override void Run(AppHostBase appHost) 
        {
            Console.Error.WriteLine(Resources.NO_HOSTING);
            Environment.Exit(-1);
        }
    }
}
