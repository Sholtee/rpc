/********************************************************************************
* AssemblyAttributes.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Resources;
using System.Runtime.CompilerServices;

[
    assembly:
        NeutralResourcesLanguage("en")
#if DEBUG
        , 
        InternalsVisibleTo("Solti.Utils.Rpc.Tests")
#endif
]
