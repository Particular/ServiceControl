// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProfileInfo
    {
        public int dwSize;
        public int dwFlags;
        [MarshalAs(UnmanagedType.LPTStr)] public String lpUserName;
        [MarshalAs(UnmanagedType.LPTStr)] public String lpProfilePath;
        [MarshalAs(UnmanagedType.LPTStr)] public String lpDefaultPath;
        [MarshalAs(UnmanagedType.LPTStr)] public String lpServerName;
        [MarshalAs(UnmanagedType.LPTStr)] public String lpPolicyPath;
        public IntPtr hProfile;
    }
}