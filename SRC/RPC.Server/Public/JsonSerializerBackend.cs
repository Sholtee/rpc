/********************************************************************************
* JsonSerializerBackend.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc
{
    using Interfaces;
    using Internals;

    /// <summary>
    /// The default <see cref="IJsonSerializer"/> implementation.
    /// </summary>
    /// <remarks>It uses the built-in <see cref="JsonSerializer"/> class.</remarks>
    public class JsonSerializerBackend : IJsonSerializer
    {
        /// <summary>
        /// Settings to be passed to the underlying <see cref="JsonSerializer"/>. 
        /// </summary>
        public JsonSerializerOptions Options { get; set; } = new JsonSerializerOptions();

        /// <inheritdoc/>
        public Task<object?[]> DeserializeMultiTypeArrayAsync(IReadOnlyList<Type> elementTypes, Stream input, CancellationToken cancellation = default)
        {
            JsonSerializerOptions options = new(Options);
            options.Converters.Add(new MultiTypeArrayConverter(elementTypes, cancellation));

            //
            // Sose NULL amit visszakapunk (lasd MultiTypeArrayConverter)
            //

            return JsonSerializer.DeserializeAsync<object?[]>(input, options, cancellation).AsTask()!;
        }
#if DEBUG
        /// <summary>
        /// This method is for testing purposes only.
        /// </summary>
        public object?[] DeserializeMultiTypeArray(IReadOnlyList<Type> elementTypes, string input)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(input);
            using MemoryStream stm = new(byteArray);
            return DeserializeMultiTypeArrayAsync(elementTypes, stm, default).GetAwaiter().GetResult();
        }
#endif
        /// <inheritdoc/>
        public Task SerializeAsync(Type type, object? obj, Stream output, CancellationToken cancellation = default)
        {
            JsonSerializerOptions options = new(Options);
            options.Converters.Add(new ServiceEntryConverter());
            options.Converters.Add(new TypeConverter());
            return JsonSerializer.SerializeAsync(output, obj, type, options, cancellation);
        }
#if DEBUG
        /// <summary>
        /// This method is for testing purposes only.
        /// </summary>
        public string Serialize<T>(T? obj)
        {
            using MemoryStream stm = new();
            SerializeAsync(typeof(T), obj, stm).GetAwaiter().GetResult();

            stm.Seek(0, SeekOrigin.Begin);

            using StreamReader sr = new(stm);
            return sr.ReadToEnd();
        }
#endif
    }
}