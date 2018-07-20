// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public UInt32 Attributes;
    }
}