namespace ServiceControlInstaller.Engine.UrlAcl.Api
{
    internal static class HttpApiConstants
    {
        public const uint InitializeConfig = 0x00000002;

        public static HttpApiVersion Version1 => new HttpApiVersion(1, 0);
    }
}
