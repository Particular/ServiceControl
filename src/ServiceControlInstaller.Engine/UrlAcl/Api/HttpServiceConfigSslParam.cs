// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct HttpServiceConfigSslParam
    {
        public int SslHashLength;
        
        public IntPtr SslHash;
        
        public Guid AppId;
        
        public string SslCertStoreName;
        
        public uint DefaultCertCheckMode;

        public int DefaultRevocationFreshnessTime;
        
        public int DefaultRevocationUrlRetrievalTimeout;
        
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DefaultSslCtlIdentifier;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string DefaultSslCtlStoreName;

        public uint DefaultFlags;
    }
}
