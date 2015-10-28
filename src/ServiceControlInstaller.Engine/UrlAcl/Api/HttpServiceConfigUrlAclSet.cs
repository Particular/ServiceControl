
namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct HttpServiceConfigUrlAclSet
    {
        public HttpServiceConfigUrlAclKey KeyDesc;

        public HttpServiceConfigUrlAclParam ParamDesc;
    }
}
