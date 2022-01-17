/********************************************************************************
* MultiTypeArraySerializer.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using Properties;

    internal sealed class MultiTypeArraySerializer // szal biztos
    {
        public static Task<object?[]> DeserializeAsync(Stream json, JsonSerializerOptions serializerOptions, IReadOnlyList<Type> elementTypes, in CancellationToken cancellation)
        {
#if FALSE
            JsonSerializerOptions opts =  = new JsonSerializerOptions(serializerOptions); // .NET 5-ben elvileg mar lesz
            opts.Converters.Add(new MultiTypeArrayConverter(elementTypes, cancellation));
#else
            JsonSerializerOptions opts = new()
            {
                AllowTrailingCommas         = serializerOptions.AllowTrailingCommas,
                DefaultBufferSize           = serializerOptions.DefaultBufferSize,
                DictionaryKeyPolicy         = serializerOptions.DictionaryKeyPolicy,
                Encoder                     = serializerOptions.Encoder,
                IgnoreNullValues            = serializerOptions.IgnoreNullValues,
                IgnoreReadOnlyProperties    = serializerOptions.IgnoreReadOnlyProperties,
                MaxDepth                    = serializerOptions.MaxDepth,
                PropertyNameCaseInsensitive = serializerOptions.PropertyNameCaseInsensitive,
                PropertyNamingPolicy        = serializerOptions.PropertyNamingPolicy,
                ReadCommentHandling         = serializerOptions.ReadCommentHandling,
                WriteIndented               = serializerOptions.WriteIndented
            };

            foreach (JsonConverter converter in serializerOptions.Converters.Append(new MultiTypeArrayConverter(elementTypes, cancellation)))
            {
                opts.Converters.Add(converter);
            }
#endif
            return JsonSerializer.DeserializeAsync<object?[]>(json, opts, cancellation).AsTask()!; // sose NULL, lasd MultiTypeArrayConverter
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
            public IReadOnlyList<Type> ElementTypes { get; }

            public CancellationToken Cancellation { get; }

            public MultiTypeArrayConverter(IReadOnlyList<Type> elementTypes, CancellationToken cancellation)
            {
                ElementTypes = elementTypes;
                Cancellation = cancellation;
            }

            public override object?[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                //
                // Csak tomboket tamogatunk
                //

                if (reader.TokenType is not JsonTokenType.StartArray)
                    throw new JsonException(Errors.NOT_AN_ARRAY);

                object?[] result = new object?[ElementTypes.Count];

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
                    // Elem deszerializalasa es rogzitese
                    //

                    Cancellation.ThrowIfCancellationRequested();

                    result[i] = JsonSerializer.Deserialize(ref reader, ElementTypes[i], options);
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
