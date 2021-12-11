/********************************************************************************
* Win32ServiceDescriptor.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Collections.Generic;

namespace Solti.Utils.Rpc.Hosting
{
    /// <summary>
    /// Describes a WIN32 service.
    /// </summary>
    public sealed class Win32ServiceDescriptor
    {
        /// <summary>
        /// The name of the service.
        /// </summary>
        public string Name { get; set; } = "MyService";

        /// <summary>
        /// Set to true if the service should start on system boot.
        /// </summary>
        public bool AutoStart { get; set; }

        /// <summary>
        /// The description of the service.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The service dependencies.
        /// </summary>
        public ICollection<string> Dependencies { get; } = new HashSet<string>();
    }
}
