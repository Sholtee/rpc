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

    internal static class MultiTypeArraySerializer
    {
        public static Task<object?[]> Deserialize(Stream json, CancellationToken cancellation, params Type[] elementTypes) 
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new MultiTypeArrayConverter(elementTypes));

            return JsonSerializer
                .DeserializeAsync<object?[]>(json, options, cancellation)
                .AsTask();
        }
#if DEBUG
        public static object?[] Deserialize(string json, params Type[] elementTypes) 
        {
            using var stm = new MemoryStream();
            using var sw = new StreamWriter(stm);
            sw.Write(json);
            sw.Flush();
            stm.Seek(0, SeekOrigin.Begin);

            return Deserialize(stm, default, elementTypes).GetAwaiter().GetResult();
        }
#endif
        private sealed class MultiTypeArrayConverter : JsonConverter<object[]>
        {
            public IReadOnlyList<Type> ElementTypes { get; }

            public MultiTypeArrayConverter(IReadOnlyList<Type> elementTypes) => ElementTypes = elementTypes;

            public override object[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                //
                // Csak tomboket tamogatunk
                //

                if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException(Errors.NOT_AN_ARRAY);

                object[] result = new object[ElementTypes.Count];

                for (int i = 0; reader.Read(); i++)
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        //
                        // A tomb hossza kissebb mint az elvart.
                        //

                        if (i < result.Length) throw new JsonException(Errors.INAPPROPRIATE_ARRAY_LENGTH);

                        return result;
                    }

                    //
                    // A tomb hossza nagyobb mint az elvart
                    //

                    if (i == result.Length) throw new JsonException(Errors.INAPPROPRIATE_ARRAY_LENGTH);

                    //
                    // Elem deszerializalasa es rogzitese
                    //

                    result[i] = JsonSerializer.Deserialize(ref reader, ElementTypes[i], options);
                }

                //
                // A JSON string rosszul formazott (nem "]"-al fejezodik be).
                //

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, object[] value, JsonSerializerOptions options) => throw new NotImplementedException();
        }
    }
}
