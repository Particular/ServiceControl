// ReSharper disable MemberCanBePrivate.Global

namespace ServiceControlInstaller.Engine.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LsaObjectAttributes
    {
        internal uint Length;
        internal IntPtr RootDirectory;
        internal LsaUnicodeString ObjectName;
        internal uint Attributes;
        internal IntPtr SecurityDescriptor;
        internal IntPtr SecurityQualityOfService;
    }
}