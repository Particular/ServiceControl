// ReSharper disable MemberCanBePrivate.Global
namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct HttpServiceConfigSslQuery
    {
        public HttpServiceConfigQueryType QueryDesc;

        public HttpServiceConfigSslKey KeyDesc;

        public uint Token;
    }
}
