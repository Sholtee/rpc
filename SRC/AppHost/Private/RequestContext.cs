/********************************************************************************
* RequestContext.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.AppHost
{
    internal class RequestContext: IRequestContext
    {
        public RequestContext(string? sessionId, string module, string method, string args) 
        {
            SessionId = sessionId;
            Module    = module  ?? throw new ArgumentNullException(nameof(module));
            Method    = method  ?? throw new ArgumentNullException(nameof(method));
            Args      = args    ?? throw new ArgumentNullException(nameof(args));  
        }

        public string? SessionId { get; }

        public string Module { get; }

        public string Method { get;  }
        
        public string Args { get; }
    }
}
