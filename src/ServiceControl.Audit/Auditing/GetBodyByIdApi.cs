namespace ServiceControl.Audit.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using BodyStorage;
    using MessagesView;
    using Raven.Abstractions.Data;
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
            string contentType;
            int bodySize;

            //We want to continue using attachments for now
#pragma warning disable 618
            var result = await bodyStorage.TryFetch(messageId).ConfigureAwait(false);
#pragma warning restore 618
            Etag currentEtag;

            HttpContent content;
            if (!result.HasResult)
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

                    if (message.BodyNotStored)
                    {
                        return request.CreateResponse(HttpStatusCode.NoContent);
                    }

                    if (message.Body == null)
                    {
                        return request.CreateResponse(HttpStatusCode.NotFound);
                    }

                    content = new StringContent(message.Body);
                    contentType = message.ContentType;
                    bodySize = message.BodySize;
                    currentEtag = stats.IndexEtag;
                }
            }
            else
            {
                content = new StreamContent(result.Stream);
                contentType = result.ContentType;
                bodySize = result.BodySize;
                currentEtag = result.Etag;
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Headers.ContentLength = bodySize;
            response.Headers.ETag = new EntityTagHeaderValue($"\"{currentEtag}\"");
            response.Content = content;
            return response;
        }

        IDocumentStore documentStore;
        IBodyStorage bodyStorage;
    }
}