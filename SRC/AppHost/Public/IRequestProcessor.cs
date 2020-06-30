/********************************************************************************
* IRequestProcessor.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Net;

namespace Solti.Utils.AppHost
{
    using DI.Extensions.Aspects;
    using Internals;

    /// <summary>
    /// Describes an abstract <see cref="HttpListenerRequest"/> processor.
    /// </summary>
    [ParameterValidatorAspect]
    public interface IRequestProcessor
    {
        /// <summary>
        /// Creates a new <see cref="IRequestContext"/> from the given <see cref="HttpListenerRequest"/>.
        /// </summary>
        IRequestContext ProcessRequest([NotNull] HttpListenerRequest request);
    }
}
