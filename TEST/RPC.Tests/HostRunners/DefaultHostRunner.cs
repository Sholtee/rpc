/********************************************************************************
* DefaultHostRunner.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Interfaces;
    using Internals;
    
    [TestFixture]
    public class DefaultHostRunnerTests
    {
        private class AppHost : AppHostBase
        {
            public override string Name => throw new NotImplementedException();

            public override string Url => throw new NotImplementedException();
        }

        [Test]
        public void Start_ShouldThrow([Values(HostConfiguration.Debug, HostConfiguration.Release)] HostConfiguration configuration)
        {
            using IHost appHost = new AppHost();
            using IHostRunner hostRunner = new DefaultHostRunner(appHost, configuration);

            Assert.Throws<Exception>(hostRunner.Start);
        }
    }
}
