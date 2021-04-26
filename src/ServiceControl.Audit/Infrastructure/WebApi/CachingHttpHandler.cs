namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    class CachingHttpHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.Headers.Contains("Cache-Control"))
            {
                response.Headers.Add("Cache-Control", "private, max-age=0");
            }

            if (!response.Headers.Contains("Vary"))
            {
                response.Headers.Add("Vary", "Accept");
            }

            return response;
        }
    }
}