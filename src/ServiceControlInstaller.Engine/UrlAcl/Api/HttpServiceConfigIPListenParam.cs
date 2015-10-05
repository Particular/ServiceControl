// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct HttpServiceConfigIPListenParam
    {
        
        public ushort AddressLength;
        public IntPtr Address;
    }
}
