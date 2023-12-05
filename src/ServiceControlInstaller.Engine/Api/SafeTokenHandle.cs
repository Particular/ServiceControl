namespace ServiceControlInstaller.Engine.Api
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;
    using Microsoft.Win32.SafeHandles;

    class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        SafeTokenHandle() : base(true)
        {
        }

        [DllImport("kernel32.dll")]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr handle);

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }
}