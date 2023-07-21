namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using ServiceBus.Management.Infrastructure.Settings;

    class GetBodyByIdApi : RoutedApi<string>
    {
        public GetBodyByIdApi(IBodyStorage bodyStorage, Settings settings, Func<HttpClient> httpClientFactory)
        {
            this.bodyStorage = bodyStorage;
            Settings = settings;
            HttpClientFactory = httpClientFactory;
        }

        protected override async Task<HttpResponseMessage> LocalQuery(HttpRequestMessage request, string input, string instanceId)
        {
            var messageId = input;

            var result = await bodyStorage.TryFetch(messageId);

            if (result == null)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            if (!result.HasResult)
            {
                return request.CreateResponse(HttpStatusCode.NoContent);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            var content = new StreamContent(result.Stream);
            MediaTypeHeaderValue.TryParse(result.ContentType, out var parsedContentType);
            content.Headers.ContentType = parsedContentType ?? new MediaTypeHeaderValue("text/*");
            content.Headers.ContentLength = result.BodySize;
            response.Headers.ETag = new EntityTagHeaderValue($"\"{result.Etag}\"");
            response.Content = content;
            return response;
        }

        readonly IBodyStorage bodyStorage;
    }
}
