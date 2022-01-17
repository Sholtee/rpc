/********************************************************************************
* MultiTypeArraySerializer.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Properties;

    internal static class MultiTypeArraySerializer // szal biztos
    {
        public static Task<object?[]> DeserializeAsync(Stream json, JsonSerializerOptions serializerOptions, IReadOnlyList<Type> elementTypes, in CancellationToken cancellation)
        {
            serializerOptions = new JsonSerializerOptions(serializerOptions);
            serializerOptions.Converters.Add(new MultiTypeArrayConverter(elementTypes, cancellation));

            return JsonSerializer.DeserializeAsync<object?[]>(json, serializerOptions, cancellation).AsTask()!; // sose NULL, lasd MultiTypeArrayConverter
        }
#if DEBUG
        public static object?[] Deserialize(string json, JsonSerializerOptions serializerOptions, IReadOnlyList<Type> elementTypes) 
        {
            using MemoryStream stm = new();
            using StreamWriter sw = new(stm);

            sw.Write(json);
            sw.Flush();
            stm.Seek(0, SeekOrigin.Begin);

            return DeserializeAsync(stm, serializerOptions, elementTypes, default).GetAwaiter().GetResult();
        }
#endif
        private sealed class MultiTypeArrayConverter : JsonConverter<object?[]>
        {
            private readonly IReadOnlyList<Type> FElementTypes;

            private readonly CancellationToken FCancellation;

            private bool FConversionStarted;

            public MultiTypeArrayConverter(IReadOnlyList<Type> elementTypes, in CancellationToken cancellation)
            {
                FElementTypes = elementTypes;
                FCancellation = cancellation;
            }

            public override bool CanConvert(Type typeToConvert) => !FConversionStarted && typeToConvert == typeof(object?[]);

            public override object?[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                //
                // A tovabbiakban mar nem akarjuk ezt a konvertert hasznalni viszont az "options.Converters"
                // lista mar nem modosithato [tehat az "options.Converters.Remove(this)" nem jatszik].
                //

                FConversionStarted = true;

                //
                // Csak tomboket tamogatunk
                //

                if (reader.TokenType is not JsonTokenType.StartArray)
                    throw new JsonException(Errors.NOT_AN_ARRAY);

                object?[] result = new object?[FElementTypes.Count];

                for (int i = 0; reader.Read(); i++)
                {
                    if (reader.TokenType is JsonTokenType.EndArray)
                    {
                        //
                        // A tomb hossza kissebb mint az elvart.
                        //

                        if (i < result.Length)
                            throw new JsonException(Errors.INAPPROPRIATE_ARRAY_LENGTH);

                        return result;
                    }

                    //
                    // A tomb hossza nagyobb mint az elvart
                    //

                    if (i == result.Length)
                        throw new JsonException(Errors.INAPPROPRIATE_ARRAY_LENGTH);

                    //
                    // A Deserialize()-nak nincs overload-ja ami tamogatna a megszakitast
                    //

                    FCancellation.ThrowIfCancellationRequested();

                    //
                    // Elem deszerializalasa es rogzitese
                    //

                    result[i] = JsonSerializer.Deserialize(ref reader, FElementTypes[i], options);
                }

                //
                // A JSON string rosszul formazott (nem "]"-al fejezodik be).
                //

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, object?[] value, JsonSerializerOptions options) => throw new NotImplementedException();
        }
    }
}
