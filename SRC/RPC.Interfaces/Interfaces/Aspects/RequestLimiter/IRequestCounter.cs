/********************************************************************************
* IRequestCounter.cs                                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Request counter service.
    /// </summary>
    public interface IRequestCounter
    {
        /// <summary>
        /// Registers a request.
        /// </summary>
        void RegisterRequest(string id, DateTime whenUtc);

        /// <summary>
        /// Registers a request.
        /// </summary>
        Task RegisterRequestAsync(string id, DateTime whenUtc, CancellationToken cancellation = default);

        /// <summary>
        /// Counts the request in a given period of time.
        /// </summary>
        int CountRequest(string id, DateTime fromUtc, DateTime toUtc);

        /// <summary>
        /// Counts the request in a given period of time.
        /// </summary>
        Task<int> CountRequestAsync(string id, DateTime fromUtc, DateTime toUtc, CancellationToken cancellation = default);
    }
}
