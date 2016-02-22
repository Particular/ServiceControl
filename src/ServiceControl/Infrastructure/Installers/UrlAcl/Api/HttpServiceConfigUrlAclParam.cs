namespace ServiceBus.Management.Infrastructure.Installers.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct HttpServiceConfigUrlAclParam
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string StringSecurityDescriptor;
        
        public HttpServiceConfigUrlAclParam(string securityDescriptor)
        {
            StringSecurityDescriptor = securityDescriptor;
        }
    }
}
