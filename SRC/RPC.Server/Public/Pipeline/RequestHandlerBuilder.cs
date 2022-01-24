/********************************************************************************
* RequestHandlerBuilder.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// Builds <see cref="IRequestHandler"/> instances.
    /// </summary>
    public abstract class RequestHandlerBuilder: IBuilder<IRequestHandler>.IParameterizedBuilder<IRequestHandler>
    {
        /// <summary>
        /// Creates a new <see cref="RequestHandlerBuilder"/> isntance.
        /// </summary>
        protected RequestHandlerBuilder(WebServiceBuilder webServiceBuilder)
        {
            WebServiceBuilder = webServiceBuilder ?? throw new ArgumentNullException(nameof(webServiceBuilder));
            WebServiceBuilder.Pipe.ApplyProxy((_, _, next) => Build((IRequestHandler) next));
        }

        /// <summary>
        /// Creates a new <see cref="IRequestHandler"/> instance.
        /// </summary>
        /// <remarks>You should not call this method directly.</remarks>
        [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration")]
        public abstract IRequestHandler Build(IRequestHandler next);

        /// <summary>
        /// The <see cref="Rpc.WebServiceBuilder"/> that instantiated this class.
        /// </summary>
        public WebServiceBuilder WebServiceBuilder { get; }
    }
}
