/********************************************************************************
* Serializer.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text.Json;

using NUnit.Framework;

namespace Solti.Utils.Rpc.Tests
{
    using DI.Interfaces;

    [TestFixture]
    public class SerializerTests
    {
        private struct MyClass 
        {
            public string Foo { get; set; }
        }

        public JsonSerializerBackend Serializer { get; set; }

        [SetUp]
        public void Setup() => Serializer = new JsonSerializerBackend();

        public static IEnumerable<(string Json, Type[] Types, object[] Result)> TestCases 
        {
            get 
            {
                yield return ("[]", Array.Empty<Type>(), new object[0]);
                yield return ("[\"cica\"]", new[] { typeof(string) }, new object[] { "cica" });
                yield return ("[null]", new[] { typeof(string) }, new object[] { null });
                yield return ("[\"cica\", 1986]", new[] { typeof(string), typeof(int) }, new object[] { "cica", 1986 });
                yield return ("[{\"Foo\": \"Bar\"}]", new[] { typeof(MyClass) }, new object[] { new MyClass { Foo = "Bar" } });
                yield return ("[\"cica\", {\"Foo\": \"Bar\"}]", new[] { typeof(string), typeof(MyClass) }, new object[] { "cica", new MyClass { Foo = "Bar"} });
            }
        }

        [TestCaseSource(nameof(TestCases))]
        public void DeserializeMultiTypeArray_ShouldWorkWith((string Json, Type[] Types, object[] Result) testCase) 
        {
            object[] result = Serializer.DeserializeMultiTypeArray(testCase.Types, testCase.Json);

            Assert.That(result.Length, Is.EqualTo(testCase.Result.Length));

            for (int i = 0; i < result.Length; i++)
            {
                Assert.That(result[i], Is.EqualTo(testCase.Result[i]));
            }
        }

        [TestCase("[]")]
        [TestCase("[1, 2]")]
        public void DeserializeMultiTypeArray_ShouldThrowOnInvalidLength(string jsonString) => Assert.Throws<JsonException>(() => Serializer.DeserializeMultiTypeArray(new[] { typeof(int) }, jsonString));

        [TestCase("{}")]
        [TestCase("{\"0\": 1}")]
        public void DeserializeMultiTypeArray_ShouldThrowOnInvalidJson(string jsonString) => Assert.Throws<JsonException>(() => Serializer.DeserializeMultiTypeArray(new[] { typeof(int) }, jsonString));

        [Test]
        public void Serialize_ShouldSerializeTypes() =>
            Assert.That(Serializer.Serialize(new { Type = typeof(int) }), Is.EqualTo("{\"Type\":\"System.Int32\"}"));

        [Test]
        public void Serialize_ShouldSerializeServiceEntries()
        {
            AbstractServiceEntry entry = new MissingServiceEntry(typeof(IModifiedServiceCollection), null);
            Assert.That(Serializer.Serialize(entry), Is.EqualTo($"\"{entry}\""));
        }
    }
}
