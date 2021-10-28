/********************************************************************************
* WebServiceDescriptor.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// 
    /// </summary>
    public class WebServiceDescriptor
    {
        /// <summary>
        /// The URL on which the WEB server will listen.
        /// </summary>
        public string Url { get; init; } = "http://localhost:1986";

        /// <summary>
        /// The maximum amount of time that is available for the service to serve a request.
        /// </summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Cross-origin_resource_sharing
        /// </summary>
        public IReadOnlyList<string> AllowedOrigins { get; init; } = new List<string>();
    }
}