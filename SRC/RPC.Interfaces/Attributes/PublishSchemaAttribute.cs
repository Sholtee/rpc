/********************************************************************************
* PublishSchemaAttribute.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Rpc.Interfaces
{
    /// <summary>
    /// Indicates that the schema of the annotated module should be published.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class PublishSchemaAttribute: Attribute { }
}
