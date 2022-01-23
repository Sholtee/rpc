/********************************************************************************
* IBuilder.cs                                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Defines a service that is responsible for building another services.
    /// </summary>
    public interface IBuilder<TBuiltService, TParam>
    {
        /// <summary>
        /// Builds a new <typeparamref name="TBuiltService"/> instance.
        /// </summary>
        TBuiltService Build(TParam param);
    }
}
