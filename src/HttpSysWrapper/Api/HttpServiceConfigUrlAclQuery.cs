// ReSharper disable MemberCanBePrivate.Global
namespace HttpApiWrapper.Api
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
