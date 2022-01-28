/********************************************************************************
* IJsonSerializer.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes an abstract JSON serializer.
    /// </summary>
    /// <remarks>The implementation should be thread safe.</remarks>
    public interface IJsonSerializer
    {
        /// <summary>
        /// Deserializes a multi-type array.
        /// </summary>
        Task<object?[]> DeserializeMultiTypeArrayAsync(IReadOnlyList<Type> elementTypes, Stream input, CancellationToken cancellation = default);

        /// <summary>
        /// Serailizes an object.
        /// </summary>
        Task SerializeAsync(Type type, object? obj, Stream output, CancellationToken cancellation = default);
    }
}
