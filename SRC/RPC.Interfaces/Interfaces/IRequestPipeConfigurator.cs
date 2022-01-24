/********************************************************************************
* IRequestPipeConfigurator.cs                                                   *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes how to configure the request pipeline.
    /// </summary>
    public interface IRequestPipeConfigurator
    {
        /// <summary>
        /// Uses the given <typeparamref name="TRequestHandlerBuilder"/> to configure the request pipe.
        /// </summary>
        IRequestPipeConfigurator Use<TRequestHandlerBuilder>(Action<TRequestHandlerBuilder>? configCallback = null) where TRequestHandlerBuilder : IBuilder<IRequestHandler>.IParameterizedBuilder<IRequestHandler>;
    }
}
