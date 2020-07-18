/********************************************************************************
* ProcessExtensions.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Solti.Utils.Rpc.Hosting.Internals
{
    internal static class ProcessExtensions
    {
        /// <summary>
        /// https://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        [SuppressMessage("Design", "CA1060:Move pinvokes to native methods class")]
        [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types")]
        private struct ProcessBasicInformation
        {
            //
            // PROCESS_BASIC_INFORMATION
            //

            private IntPtr Reserved1;
            private IntPtr PebBaseAddress;
            private IntPtr Reserved2_0;
            private IntPtr Reserved2_1;
            private IntPtr UniqueProcessId;
            private IntPtr InheritedFromUniqueProcessId;

            [DllImport("ntdll.dll")]
            private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

            public static Process? GetParentProcess(IntPtr handle)
            {
                ProcessBasicInformation pbi = new ProcessBasicInformation();

                int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
                if (status != 0)
                    throw new Win32Exception(status);

                try
                {
                    return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
                }
                catch (ArgumentException)
                {
                    // not found
                    return null;
                }
            }
        }

        public static Process? GetParent(this Process src) 
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            return ProcessBasicInformation.GetParentProcess(src.Handle);
        }
    }
}
