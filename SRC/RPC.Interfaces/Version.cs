/********************************************************************************
* Version.cs                                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Represents a version number.
    /// </summary>
    public class Version
    {
        /// <summary>
        /// Converts the given <see cref="FileVersionInfo"/> to <see cref="Version"/>.
        /// </summary>
        [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")]
        public static implicit operator Version(FileVersionInfo src)
        {
            if (src is null) return null!;

            return new Version
            {
                Major = src.ProductMajorPart,
                Minor = src.ProductMinorPart,
                Patch = src.ProductBuildPart
            };
        }

        /// <summary>
        /// Converts the given <see cref="System.Version"/> to <see cref="Version"/>.
        /// </summary>
        [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")]
        public static implicit operator Version(System.Version src)
        {
            if (src is null) return null!;

            return new Version
            {
                Major = src.Major,
                Minor = src.Minor,
                Patch = src.Build
            };
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is not Version that) return false;

            return that.Major == Major && that.Minor == Minor && that.Patch == Patch;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => new { Major, Minor, Patch }.GetHashCode();

        /// <summary>
        /// The major version.
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// The minor version.
        /// </summary>
        public int Minor { get; set; }
        
        /// <summary>
        /// The patch version.
        /// </summary>
        public int Patch { get; set; }
    }
}
