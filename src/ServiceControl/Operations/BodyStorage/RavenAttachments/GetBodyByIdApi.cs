namespace ServiceControl.Operations.BodyStorage.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Raven.Client;

    class GetBodyByIdApi : RoutedApi<string>
    {
        public GetBodyByIdApi(IDocumentStore documentStore, IBodyStorage bodyStorage)
        {
            this.documentStore = documentStore;
            this.bodyStorage = bodyStorage;
        }

        protected override async Task<HttpResponseMessage> LocalQuery(HttpRequestMessage request, string input, string instanceId)
        {
            var messageId = input;
            messageId = messageId?.Replace("/", @"\");
            var indexResponse = await TryFetchFromIndex(request, messageId).ConfigureAwait(false);
            // when fetching result from index is successful go ahead
            if (indexResponse.IsSuccessStatusCode)
            {
                return indexResponse;
            }

            // try to fetch from body
            var bodyStorageResponse = await TryFetchFromStorage(request, messageId).ConfigureAwait(false);
            // if found return, if not the result from the index takes precedence to be backward compatible
            return bodyStorageResponse.IsSuccessStatusCode ? bodyStorageResponse : indexResponse;
        }

        async Task<HttpResponseMessage> TryFetchFromStorage(HttpRequestMessage request, string messageId)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            var result = await bodyStorage.TryFetch(messageId).ConfigureAwait(false);
#pragma warning restore 618
            if (result.HasResult)
            {
                var response = request.CreateResponse(HttpStatusCode.OK);
                var content = new StreamContent(result.Stream);
                MediaTypeHeaderValue.TryParse(result.ContentType, out var parsedContentType);
                content.Headers.ContentType = parsedContentType ?? new MediaTypeHeaderValue("text/*");
                content.Headers.ContentLength = result.BodySize;
                response.Headers.ETag = new EntityTagHeaderValue($"\"{result.Etag}\"");
                response.Content = content;
                return response;
            }

            return request.CreateResponse(HttpStatusCode.NotFound);
        }

        async Task<HttpResponseMessage> TryFetchFromIndex(HttpRequestMessage request, string messageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
                    .FirstOrDefaultAsync(f => f.MessageId == messageId)
                    .ConfigureAwait(false);

                if (message == null)
                {
                    return request.CreateResponse(HttpStatusCode.NotFound);
                }

                if (message.BodyNotStored && message.Body == null)
                {
                    return request.CreateResponse(HttpStatusCode.NoContent);
                }

                if (message.Body == null)
                {
                    return request.CreateResponse(HttpStatusCode.NotFound);
                }

                var response = request.CreateResponse(HttpStatusCode.OK);
                var content = new StringContent(message.Body);

                MediaTypeHeaderValue.TryParse(message.ContentType, out var parsedContentType);
                content.Headers.ContentType = parsedContentType ?? new MediaTypeHeaderValue("text/*");

                content.Headers.ContentLength = message.BodySize;
                response.Headers.ETag = new EntityTagHeaderValue($"\"{stats.IndexEtag}\"");
                response.Content = content;
                return response;
            }
        }

        readonly IBodyStorage bodyStorage;
        readonly IDocumentStore documentStore;
    }
}
