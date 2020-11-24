using System;
using System.Linq;

namespace ServiceControl.Operations.BodyStorage.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Raven.Client.Documents;
    using MessageFailures;
    using System.Net.Http.Headers;

    class GetBodyByIdApi : RoutedApi<string>
    {
        public GetBodyByIdApi(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        protected override async Task<HttpResponseMessage> LocalQuery(HttpRequestMessage request, string input, string instanceId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var attachment = await session.Advanced.Attachments.GetAsync(FailedMessage.MakeDocumentId(input), "body")
                    .ConfigureAwait(false);

                string contentType;
                if (attachment == null)
                {
                    var legacyAttachment = await session.Advanced.Attachments
                        .GetAsync($"files/messagebodies/{input}", $"messagebodies/{input}")
                        .ConfigureAwait(false);
                    if (legacyAttachment != null)
                    {
                        attachment = legacyAttachment;
                        contentType = "application/octet-stream";
                    }
                    else
                    {
                        throw new Exception($"Cannot find attachment by ID {input}.");
                    }
                }
                else
                {
                    //We don't know the actual content type
                    contentType = attachment.Details.ContentType;
                }

                var response = request.CreateResponse(HttpStatusCode.OK);
                var content = new StreamContent(attachment.Stream);
                await content.LoadIntoBufferAsync(attachment.Details.Size).ConfigureAwait(false);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                content.Headers.ContentLength = attachment.Details.Size;
                //Message Id is in fact processing ID: a guid generated based on message ID, processing endpoint and processing time. It can safely be used as ETag.
                response.Headers.ETag = new EntityTagHeaderValue($"\"{input}\"");
                response.Content = content;

                return response;
            }
        }
        readonly IDocumentStore documentStore;
    }
}