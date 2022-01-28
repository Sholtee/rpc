/********************************************************************************
* ServiceEntryConverter.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;

    //
    // ServiceNotFoundException es tarsai tartalmazhatnak hivatkozast szerviz bejegyzesekre
    //

    internal sealed class ServiceEntryConverter : JsonConverter<AbstractServiceEntry>
    {
        public override bool CanConvert(Type typeToConvert) => typeof(AbstractServiceEntry).IsAssignableFrom(typeToConvert);

        public override AbstractServiceEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

        public override void Write(Utf8JsonWriter writer, AbstractServiceEntry value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value?.ToString(), options);
    }
}
