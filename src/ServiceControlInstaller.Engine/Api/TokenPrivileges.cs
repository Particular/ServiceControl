namespace ServiceControlInstaller.Engine.Api
{
    using System;
    using System.Runtime.InteropServices;

    internal struct TokenPrivileges
    {
#pragma warning disable 169
        public UInt32 PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
#pragma warning restore 169
    }
}