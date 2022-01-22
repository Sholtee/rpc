/********************************************************************************
* SafeSerializer.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Text.Json;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI.Interfaces;
    using Internals;

    [TestFixture]
    public class SafeSerializerTests
    {
        [Test]
        public void Serialize_ShouldSerializeTypes() =>
            Assert.That(SafeSerializer.Serialize(new { Type = typeof(int) }, new JsonSerializerOptions()), Is.EqualTo("{\"Type\":\"System.Int32\"}"));

        [Test]
        public void Serialize_ShouldSerializeServiceEntries()
        {
            AbstractServiceEntry entry = new MissingServiceEntry(typeof(IModifiedServiceCollection), null);
            Assert.That(SafeSerializer.Serialize(entry, new JsonSerializerOptions()), Is.EqualTo($"\"{entry}\""));
        }
    }
}
