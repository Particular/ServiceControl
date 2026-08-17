namespace ServiceControl.AcceptanceTesting
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent iContent, CancellationToken cancellationToken = default)
        {
            var method = new HttpMethod("PATCH");
            var request = new HttpRequestMessage(method, requestUri)
            {
                Content = iContent
            };

            var response = await client.SendAsync(request, cancellationToken);

            return response;
        }
    }
}