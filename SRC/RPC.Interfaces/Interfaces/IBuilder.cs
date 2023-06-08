/********************************************************************************
* IBuilder.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Defines a service that is responsible for building another services.
    /// </summary>
    public interface IBuilder<TBuiltService>
    {
        /// <summary>
        /// Defines a parameterized service that is responsible for building another services.
        /// </summary>
        public interface IParameterizedBuilder<TParam>
        {
            /// <summary>
            /// Builds a new <typeparamref name="TBuiltService"/> instance.
            /// </summary>
            TBuiltService Build(TParam param);
        }

        /// <summary>
        /// Builds a new <typeparamref name="TBuiltService"/> instance.
        /// </summary>
        TBuiltService Build();
    }
}
