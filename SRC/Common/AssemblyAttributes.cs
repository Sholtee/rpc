﻿/********************************************************************************
* AssemblyAttributes.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Resources;
using System.Runtime.CompilerServices;

[
    assembly:
        NeutralResourcesLanguage("en"),
        InternalsVisibleTo("RPC.Perf")
#if DEBUG
        , 
        InternalsVisibleTo("Solti.Utils.Rpc.Tests")
#endif
]