/********************************************************************************
* RequestHandlerFactory.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/

#pragma warning disable CA1716 // Identifiers should not match keywords
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;

    /// <summary>
    /// Creates <see cref="IRequestHandler"/> instances.
    /// </summary>
    public abstract class RequestHandlerFactory
    {
        /// <summary>
        /// Called once the factory configuration is finished. 
        /// </summary>
        /// <remarks>You should not call this method directly.</remarks>
        protected internal virtual void FinishConfiguration() => WebServiceBuilder.Pipe.ApplyProxy((_, _, next) => Create((IRequestHandler) next));

        /// <summary>
        /// Creates a new <see cref="IRequestHandler"/> instance.
        /// </summary>
        /// <remarks>You should not call this method directly.</remarks>
        protected abstract IRequestHandler Create(IRequestHandler next);

        /// <summary>
        /// The <see cref="Rpc.WebServiceBuilder"/> that instantiated this class.
        /// </summary>
        public WebServiceBuilder WebServiceBuilder { get; init; }
    }
}
