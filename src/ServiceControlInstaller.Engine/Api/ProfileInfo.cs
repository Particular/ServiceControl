namespace ServiceControlInstaller.Engine.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    struct ProfileInfo
    {
        public int dwSize;
        public int dwFlags;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpUserName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpProfilePath;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpDefaultPath;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpServerName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpPolicyPath;
        public IntPtr hProfile;
    }
}