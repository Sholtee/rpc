/********************************************************************************
* ProcessExtensions.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Hosting.Tests
{
    using Internals;
    
    [TestFixture]
    public class ProcessExtensionsTests
    {
        [Test]
        public void GetParent_ShouldReturnTheParent() 
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("The related feature is Windows exclusive.");

            Process parent = ProcessExtensions.GetParent();

            Assert.That(parent, Is.Not.Null);
            Assert.That(parent.MainModule.FileName, Is.Not.EqualTo(Process.GetCurrentProcess().MainModule.FileName));
        }
    }
}
