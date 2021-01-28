namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    struct HttpServiceConfigIPListenParam
    {
        public ushort AddressLength;
        public IntPtr Address;
    }
}