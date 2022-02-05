/********************************************************************************
* TypeExtensions.cs                                                             *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Internals
{
    internal static class TypeExtensions
    {
        internal static IEnumerable<MethodInfo> GetAllInterfaceMethods(this Type iface)
        {
            Debug.Assert(iface.IsInterface);

            //
            // A "BindingFlags.FlattenHierarchy" interface-ekre nem mukodik
            //

            return iface
                .GetMethods(BindingFlags.Instance | BindingFlags.Public /* | BindingFlags.FlattenHierarchy*/)
                .Concat
                (
                    iface.GetInterfaces().SelectMany(GetAllInterfaceMethods)
                )

                //
                // IIface: IA, IB ahol IA: IC es IB: IC -> Distinct()
                //

                .Distinct();
        }

        internal static IEnumerable<PropertyInfo> GetAllInterfaceProperties(this Type iface)
        {
            Debug.Assert(iface.IsInterface);

            //
            // A "BindingFlags.FlattenHierarchy" interface-ekre nem mukodik
            //

            return iface
                .GetProperties(BindingFlags.Instance | BindingFlags.Public /* | BindingFlags.FlattenHierarchy*/)
                .Concat
                (
                    iface.GetInterfaces().SelectMany(GetAllInterfaceProperties)
                )

                //
                // IIface: IA, IB ahol IA: IC es IB: IC -> Distinct()
                //

                .Distinct();
        }
    }
}
