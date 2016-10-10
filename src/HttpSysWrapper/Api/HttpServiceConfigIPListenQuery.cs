
// ReSharper disable MemberCanBePrivate.Global
namespace HttpApiWrapper.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct HttpServiceConfigIPListenQuery
    {
        public int AddressCount;
        public IntPtr AddressList;
    }
}
