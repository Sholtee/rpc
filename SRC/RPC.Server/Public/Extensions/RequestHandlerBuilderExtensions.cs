/********************************************************************************
* RequestHandlerBuilderExtensions.cs                                            *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Pipeline
{
    /// <summary>
    /// <see cref="RequestHandlerBuilder"/> related extensions.
    /// </summary>
    public static class RequestHandlerBuilderExtensions
    {
        /// <summary>
        /// Gets the parent of a builder.
        /// </summary>
        public static TParent? GetParent<TParent>(this RequestHandlerBuilder src) where TParent : RequestHandlerBuilder
        {
            if (src is null)
                throw new ArgumentNullException(nameof(src));

            do
            {
                if (src is TParent parent)
                    return parent;

                src = src.Parent!;
            } while (src is not null);

            return null;
        }
    }
}