/********************************************************************************
* MultiTypeArraySerializer.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solti.Utils.Rpc.Internals
{
    using Properties;

    internal static class MultiTypeArraySerializer
    {
        public static object[] Deserialize(string jsonString, params Type[] elementTypes) 
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new MultiTypeArrayConverter(elementTypes));

            return JsonSerializer.Deserialize<object[]>(jsonString, options);
        }

        private sealed class MultiTypeArrayConverter : JsonConverter<object[]>
        {
            public IReadOnlyList<Type> ElementTypes { get; }

            public MultiTypeArrayConverter(IReadOnlyList<Type> elementTypes) => ElementTypes = elementTypes;

            public override object[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                //
                // Csak tomboket tamogatunk
                //

                if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException(Resources.NOT_AN_ARRAY);

                object[] result = new object[ElementTypes.Count];

                for (int i = 0; reader.Read(); i++)
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        //
                        // A tomb hossza kissebb mint az elvart.
                        //

                        if (i < result.Length) throw new JsonException(Resources.INAPPROPRIATE_ARRAY_LENGTH);

                        return result;
                    }

                    //
                    // A tomb hossza nagyobb mint az elvart
                    //

                    if (i == result.Length) throw new JsonException(Resources.INAPPROPRIATE_ARRAY_LENGTH);

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
