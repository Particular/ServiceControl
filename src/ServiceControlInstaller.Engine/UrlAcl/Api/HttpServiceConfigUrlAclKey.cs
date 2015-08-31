namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct HttpServiceConfigUrlAclKey
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string UrlPrefix;

        public HttpServiceConfigUrlAclKey(string urlPrefix)
        {
            UrlPrefix = urlPrefix;
        }
    }
}
