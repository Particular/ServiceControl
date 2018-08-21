namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    internal static class HttpApiConstants
    {
        public static HttpApiVersion Version1 => new HttpApiVersion(1, 0);
        public const uint InitializeConfig = 0x00000002;
    }
}