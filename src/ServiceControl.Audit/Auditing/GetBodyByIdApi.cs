namespace ServiceControl.Audit.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using BodyStorage;
    using MessagesView;
    using Raven.Client;

    class GetBodyByIdApi : IApi
    {
        public GetBodyByIdApi(IDocumentStore documentStore, IBodyStorage bodyStorage)
        {
            this.documentStore = documentStore;
            this.bodyStorage = bodyStorage;
        }

        public async Task<HttpResponseMessage> Execute(HttpRequestMessage request, string messageId)
        {
            messageId = messageId?.Replace("/", @"\");
            var indexResponse = await TryFetchFromIndex(request, messageId).ConfigureAwait(false);
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
#pragma warning disable 618
            var result = await bodyStorage.TryFetch(documentStore, messageId).ConfigureAwait(false);
#pragma warning restore 618
            if (result.HasResult)
            {
                var response = request.CreateResponse(HttpStatusCode.OK);
                var content = new StreamContent(result.Stream);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(result.ContentType);
                content.Headers.ContentLength = result.BodySize;
                response.Headers.ETag = new EntityTagHeaderValue($"\"{result.Etag}\"");
                response.Content = content;
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

                var response = request.CreateResponse(HttpStatusCode.OK);
                var content = new StringContent(message.Body);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(message.ContentType);
                content.Headers.ContentLength = message.BodySize;
                response.Headers.ETag = new EntityTagHeaderValue($"\"{stats.IndexEtag}\"");
                response.Content = content;
                return response;
            }
        }


        IDocumentStore documentStore;
        IBodyStorage bodyStorage;
    }
}