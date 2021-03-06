﻿/********************************************************************************
* AssemblyAttributes.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Resources;
using System.Runtime.CompilerServices;

[
    assembly:
        NeutralResourcesLanguage("en"),
        CLSCompliant(true),
        InternalsVisibleTo("RPC.Perf")
#if DEBUG
        , 
        InternalsVisibleTo("Solti.Utils.Rpc.Tests")
#endif
]
