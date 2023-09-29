﻿namespace ServiceControlInstaller.Engine.Api
{
    using System.Runtime.InteropServices;

    struct TokenPrivileges
    {
#pragma warning disable 169
        public uint PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
#pragma warning restore 169
    }
}