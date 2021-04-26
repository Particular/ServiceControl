namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    class XParticularVersionHttpHandler : DelegatingHandler
    {
        static XParticularVersionHttpHandler()
        {
            FileVersion = GetFileVersion();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.Headers.Add("X-Particular-Version", FileVersion);

            return response;
        }

        static string GetFileVersion()
        {
            var customAttributes = typeof(XParticularVersionHttpHandler).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);

            if (customAttributes.Length >= 1)
            {
                var fileVersionAttribute = (AssemblyInformationalVersionAttribute)customAttributes[0];
                var informationalVersion = fileVersionAttribute.InformationalVersion;
                return informationalVersion.Split('+')[0];
            }

            return typeof(XParticularVersionHttpHandler).Assembly.GetName().Version.ToString(4);
        }

        static readonly string FileVersion;
    }
}