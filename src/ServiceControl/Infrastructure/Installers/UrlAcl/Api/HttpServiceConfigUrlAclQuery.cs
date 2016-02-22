// ReSharper disable MemberCanBePrivate.Global
namespace ServiceBus.Management.Infrastructure.Installers.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct HttpServiceConfigUrlAclQuery
    {
        public HttpServiceConfigQueryType QueryDesc;

        public HttpServiceConfigUrlAclKey KeyDesc;

        public uint Token;
    }
}
