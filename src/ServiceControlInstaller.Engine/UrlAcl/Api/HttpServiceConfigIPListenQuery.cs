namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    struct HttpServiceConfigIPListenQuery
    {
        public int AddressCount;
        public IntPtr AddressList;
    }
}