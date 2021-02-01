namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    struct HttpServiceConfigSslKey
    {
        /// <summary>
        /// Pointer to the port for the IP address.
        /// </summary>
        public IntPtr IPPort;
    }
}