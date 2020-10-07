namespace ServiceControl.Operations.BodyStorage.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Raven.Client.Documents;

    class GetBodyByIdApi : RoutedApi<string>
    {
        public GetBodyByIdApi(IDocumentStore documentStore)
        {
            // this.documentStore = documentStore;
        }

        protected override async Task<HttpResponseMessage> LocalQuery(HttpRequestMessage request, string input, string instanceId)
        {
            await Task.Yield();
            return request.CreateResponse(HttpStatusCode.NotFound);

            // TODO: RAVEN5 - Body storage will change with v5
//            var messageId = input;
//            messageId = messageId?.Replace("/", @"\");
//            string contentType;
//            int bodySize;

//            //We want to continue using attachments for now
//#pragma warning disable 618
//            var result = await bodyStorage.TryFetch(messageId).ConfigureAwait(false);
//#pragma warning restore 618
//            Etag currentEtag;

//            HttpContent content;
//            if (!result.HasResult)
//            {
//                using (var session = documentStore.OpenAsyncSession())
//                {
//                    var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
//                        .Statistics(out var stats)
//                        //.TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
//                        .FirstOrDefaultAsync(f => f.MessageId == messageId)
//                        .ConfigureAwait(false);

//                    if (message == null)
//                    {
//                        return request.CreateResponse(HttpStatusCode.NotFound);
//                    }

//                    if (message.BodyNotStored)
//                    {
//                        return request.CreateResponse(HttpStatusCode.NoContent);
//                    }

//                    if (message.Body == null)
//                    {
//                        return request.CreateResponse(HttpStatusCode.NotFound);
//                    }

//                    content = new StringContent(message.Body);
//                    contentType = message.ContentType;
//                    bodySize = message.BodySize;
//                    currentEtag = stats.IndexEtag;
//                }
//            }
//            else
//            {
//                content = new StreamContent(result.Stream);
//                contentType = result.ContentType;
//                bodySize = result.BodySize;
//                currentEtag = result.Etag;
//            }

//            var response = request.CreateResponse(HttpStatusCode.OK);
//            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
//            content.Headers.ContentLength = bodySize;
//            response.Headers.ETag = new EntityTagHeaderValue($"\"{currentEtag}\"");
//            response.Content = content;
//            return response;
        }

        // readonly IBodyStorage bodyStorage;
        // readonly IDocumentStore documentStore;
    }
}