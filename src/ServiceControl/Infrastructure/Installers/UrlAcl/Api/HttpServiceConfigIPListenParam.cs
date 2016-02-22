// ReSharper disable MemberCanBePrivate.Global
namespace ServiceBus.Management.Infrastructure.Installers.UrlAcl.Api
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
