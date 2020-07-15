/********************************************************************************
* HostRunner.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;
using System.Linq;

namespace Solti.Utils.Rpc.Hosting
{
    /// <summary>
    /// Defines an abstract host runner.
    /// </summary>
    public abstract class HostRunner
    {
        /// <summary>
        /// If overridden in the derived class it should determines whether the runner should be used.
        /// </summary>
        public abstract bool ShouldUse();

        /// <summary>
        /// Runs the given host.
        /// </summary>
        public abstract void Run(AppHostBase appHost);
    }
}
