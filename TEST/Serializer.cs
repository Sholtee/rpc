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
    [TestFixture]
    public class SerializerTests
    {
        [Test]
        public void Serializer_ShouldDeserializeExceptions() 
        {
            var ex = new ExceptionInfo
            {
                Message = "cica",
                TypeName = typeof(Exception).FullName,
                Data = new Dictionary<object, object>
                {
                    { "BD", 1986 }
                }
            };

            string json = JsonSerializer.Serialize(ex);

            ExceptionInfo deserialized = null;

            Assert.DoesNotThrow(() => deserialized = JsonSerializer.Deserialize<ExceptionInfo>(json));
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Message, Is.EqualTo(ex.Message));
            Assert.That(deserialized.Data.Count, Is.EqualTo(1));
            //Assert.That(deserialized.Data["BD"], Is.EqualTo(1986));
        }
    }
}
