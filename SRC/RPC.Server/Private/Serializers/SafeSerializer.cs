/********************************************************************************
* SafeSerializer.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;

    internal static class SafeSerializer
    {
        public static Task SerializeAsync(Stream outputStream, object? value, JsonSerializerOptions serializerOptions, CancellationToken cancellation = default)
        {
            serializerOptions = new JsonSerializerOptions(serializerOptions);
            serializerOptions.Converters.Add(new TypeConverter());
            serializerOptions.Converters.Add(new ServiceEntryConverter());

            return JsonSerializer.SerializeAsync(outputStream, value, serializerOptions, cancellation);
        }
#if DEBUG
        public static string Serialize(object? value, JsonSerializerOptions serializerOptions)
        {
            using MemoryStream stm = new();
            SerializeAsync(stm, value, serializerOptions).GetAwaiter().GetResult();

            stm.Seek(0, SeekOrigin.Begin);

            using StreamReader sr = new(stm);
            return sr.ReadToEnd();
        }
#endif
        //
        // ServiceNotFoundException es tarsai tartalmazhatnak hivatkozast szerviz bejegyzesekre
        //

        internal sealed class ServiceEntryConverter : JsonConverter<AbstractServiceEntry>
        {
            public override bool CanConvert(Type typeToConvert) => typeof(AbstractServiceEntry).IsAssignableFrom(typeToConvert);

            public override AbstractServiceEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, AbstractServiceEntry value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value?.ToString(), options);
        }

        //
        // Tipusok szerializalasa nem biztonsagos (epp ezert ha ez nincs akkor a JsonSerializer NotSupportedException-t dob).
        //

        internal sealed class TypeConverter : JsonConverter<Type>
        {
            public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value?.FullName, options);
        }
    }
}
