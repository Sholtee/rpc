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
    [SuppressMessage("Design", "CA1060:Move pinvokes to native methods class")]
    [SuppressMessage("Security", "CA5392:Use DefaultDllImportSearchPaths attribute for P/Invokes")]
    internal static class ProcessExtensions
    {
        /// <summary>
        /// https://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
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

            public static int GetParentPid()
            {
                ProcessBasicInformation pbi = new ProcessBasicInformation();

                int status = NtQueryInformationProcess(Process.GetCurrentProcess().Handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
                if (status != 0)
                    throw new Win32Exception(status);

                return pbi.InheritedFromUniqueProcessId.ToInt32();
            }
        }

        [DllImport("libc", EntryPoint = "getppid")]
        private static extern int GetParentPid();

        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public static Process? Parent
        {
            get
            {
                int parentPid;

                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        parentPid = ProcessBasicInformation.GetParentPid();
                        break;
                    case PlatformID.Unix:
                        parentPid = GetParentPid();
                        break;
                    default: return null;
                };

                try
                {
                    return Process.GetProcessById(parentPid);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool IsService 
        {
            get 
            {
                Process? parent = Parent;
                if (parent == null) 
                    return false;

                return Environment.OSVersion.Platform switch
                {
                    PlatformID.Win32NT => parent.SessionId == 0 && string.Equals("services", parent.ProcessName, StringComparison.OrdinalIgnoreCase),
                    PlatformID.Unix => string.Equals("systemd", parent.ProcessName.TrimEnd('\n'), StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }
        }
    }
}
