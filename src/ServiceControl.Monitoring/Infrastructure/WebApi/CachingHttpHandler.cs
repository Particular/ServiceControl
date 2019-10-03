namespace ServiceControl.Monitoring.Infrastructure.WebApi
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    class CachingHttpHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.Content.Headers.Contains("Expires"))
            {
                response.Content.Headers.Add("Expires", "Tue, 03 Jul 2001 06:00:00 GMT");
            }

            if (!response.Content.Headers.Contains("Last-Modified"))
            {
                response.Content.Headers.Add("Last-Modified", DateTime.Now.ToUniversalTime().ToString("R"));
            }

            if (!response.Headers.Contains("Cache-Control"))
            {
                response.Headers.Add("Cache-Control", "private, max-age=0, no-cache, must-revalidate, proxy-revalidate, no-store");
            }

            if (!response.Headers.Contains("Pragma"))
            {
                response.Headers.Add("Pragma", "no-cache");
            }

            if (!response.Headers.Contains("Vary"))
            {
                response.Headers.Add("Vary", "Accept");
            }

            return response;
        }
    }
}