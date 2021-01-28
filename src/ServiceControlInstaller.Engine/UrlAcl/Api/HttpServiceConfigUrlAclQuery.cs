namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct HttpServiceConfigUrlAclQuery
    {
        public HttpServiceConfigQueryType QueryDesc;

        public HttpServiceConfigUrlAclKey KeyDesc;

        public uint Token;
    }
}