namespace ServiceControl.Operations.BodyStorage.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Microsoft.AspNetCore.Http;
    using ServiceBus.Management.Infrastructure.Settings;

    public record GetByBodyContext(string InstanceId, string MessageId) : RoutedApiContext(InstanceId);

    public class GetBodyByIdApi : RoutedApi<GetByBodyContext>
    {
        public GetBodyByIdApi(IBodyStorage bodyStorage, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
            : base(settings, httpClientFactory, httpContextAccessor)
        {
            this.bodyStorage = bodyStorage;
        }

        protected override async Task<HttpResponseMessage> LocalQuery(GetByBodyContext input)
        {
            var result = await bodyStorage.TryFetch(input.MessageId);

            if (result == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (!result.HasResult)
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK);
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
