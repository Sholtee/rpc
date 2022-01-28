/********************************************************************************
* TypeConverter.cs                                                              *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solti.Utils.Rpc.Internals
{
    //
    // Tipusok szerializalasa nem biztonsagos (epp ezert ha ez nincs akkor a JsonSerializer NotSupportedException-t dob).
    //

    internal sealed class TypeConverter : JsonConverter<Type>
    {
        public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

        public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value?.FullName, options);
    }
}
