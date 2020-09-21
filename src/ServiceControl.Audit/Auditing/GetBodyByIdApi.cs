namespace ServiceControl.Audit.Auditing
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using MessagesView;
    using Raven.Client.Documents;

    class GetBodyByIdApi : IApi
    {
        public GetBodyByIdApi(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task<HttpResponseMessage> Execute(HttpRequestMessage request, string messageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var attachment = await session.Advanced.Attachments.GetAsync($"ProcessedMessages/{messageId}", "body")
                    .ConfigureAwait(false);

                var response = request.CreateResponse(HttpStatusCode.OK);
                
                var content = new StreamContent(attachment.Stream);
                await content.LoadIntoBufferAsync(attachment.Details.Size).ConfigureAwait(false);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(attachment.Details.ContentType);
                content.Headers.ContentLength = attachment.Details.Size;
                //Message Id is in fact processing ID: a guid generated based on message ID, processing endpoint and processing time. It can safely be used as ETag.
                response.Headers.ETag = new EntityTagHeaderValue($"\"{messageId}\"");
                response.Content = content;
                
                return response;
            }
        }

        IDocumentStore documentStore;
    }
}