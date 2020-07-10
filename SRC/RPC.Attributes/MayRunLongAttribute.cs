/********************************************************************************
* MayRunLongAttribute.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc
{
    /// <summary>
    /// Informs the system that the annotated method may run long.
    /// </summary>
    /// <remarks>The system uses <see cref="TaskCreationOptions.LongRunning"/> tasks for the methods that are annotated with this attribute.</remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class MayRunLongAttribute: Attribute
    {
    }
}
