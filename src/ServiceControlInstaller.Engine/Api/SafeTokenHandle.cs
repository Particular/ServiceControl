namespace ServiceControlInstaller.Engine.Api
{
    using System;
#if NETFRAMEWORK
    using System.Runtime.ConstrainedExecution;
#endif
    using System.Runtime.InteropServices;
    using System.Security;
    using Microsoft.Win32.SafeHandles;

    class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        SafeTokenHandle() : base(true)
        {
        }

        [DllImport("kernel32.dll")]
#if NETFRAMEWORK
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#endif
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr handle);

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }
}