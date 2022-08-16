namespace ServiceControl.Audit.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using BodyStorage;
    using MessagesView;
    using ServiceControl.Audit.Persistence;

    class GetBodyByIdApi : IApi
    {
        public GetBodyByIdApi(IAuditDataStore dataStore, IBodyStorage bodyStorage)
        {
            this.dataStore = dataStore;
            this.bodyStorage = bodyStorage;
        }

        public async Task<HttpResponseMessage> Execute(HttpRequestMessage request, string messageId)
        {
            messageId = messageId?.Replace("/", @"\");
            var indexResponse = await dataStore.TryFetchFromIndex(request, messageId).ConfigureAwait(false);
            // when fetching result from index is successful go ahead
            if (indexResponse.IsSuccessStatusCode)
            {
                return indexResponse;
            }

            // try to fetch from body
            var bodyStorageResponse = await TryFetchFromStorage(request, messageId).ConfigureAwait(false);
            // if found return, if not the result from the index takes precedence to by backward compatible
            return bodyStorageResponse.IsSuccessStatusCode ? bodyStorageResponse : indexResponse;
        }

        async Task<HttpResponseMessage> TryFetchFromStorage(HttpRequestMessage request, string messageId)
        {
            //We want to continue using attachments for now
            var result = await bodyStorage.TryFetch(messageId).ConfigureAwait(false);
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

        IAuditDataStore dataStore;
        IBodyStorage bodyStorage;
    }
}