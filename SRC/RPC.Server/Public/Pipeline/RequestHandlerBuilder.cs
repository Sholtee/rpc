/********************************************************************************
* RequestHandlerBuilder.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Pipeline
{
    using Interfaces;

    /// <summary>
    /// Builds <see cref="IRequestHandler"/> instances.
    /// </summary>
    public abstract class RequestHandlerBuilder: IBuilder<IRequestHandler>.IParameterizedBuilder<IRequestHandler>
    {
        /// <summary>
        /// Creates a new <see cref="RequestHandlerBuilder"/> isntance.
        /// </summary>
        protected RequestHandlerBuilder(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent)
        {
            WebServiceBuilder = webServiceBuilder ?? throw new ArgumentNullException(nameof(webServiceBuilder));
            WebServiceBuilder.Pipe.Decorate((_, _, next) => Build((IRequestHandler) next));
            Parent = parent;
        }

        /// <summary>
        /// Creates a new <see cref="IRequestHandler"/> instance.
        /// </summary>
        /// <remarks>You should not call this method directly.</remarks>
        public abstract IRequestHandler Build(IRequestHandler next);

        /// <summary>
        /// The preceding <see cref="RequestHandlerBuilder"/>, if exists. 
        /// </summary>
        public RequestHandlerBuilder? Parent { get; }

        /// <summary>
        /// The <see cref="Rpc.WebServiceBuilder"/> that instantiated this class.
        /// </summary>
        public WebServiceBuilder WebServiceBuilder { get; }
    }
}
