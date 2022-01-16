/********************************************************************************
* IRequestPipeConfig.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Describes how to configure the request pipeline.
    /// </summary>
    public interface IRequestPipeConfigurator<TRequestHandlerBase>
    {
        /// <summary>
        /// 
        /// </summary>
        IRequestPipeConfigurator<TRequestHandlerBase> Use<TRequestHandler>(Action<TRequestHandler>? configCallback = null) where TRequestHandler: TRequestHandlerBase, new();
    }
}
