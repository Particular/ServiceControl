// ReSharper disable MemberCanBePrivate.Global
namespace HttpApiWrapper.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct HttpServiceConfigSslKey
    {
        /// <summary>
        /// Pointer to the port for the IP address.
        /// </summary>
        public IntPtr IPPort;
    }
}
