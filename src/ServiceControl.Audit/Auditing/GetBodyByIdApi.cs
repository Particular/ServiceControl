namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using MessagesView;
    using Persistence;

    class GetBodyByIdApi : IApi
    {
        public GetBodyByIdApi(IAuditDataStore dataStore) => this.dataStore = dataStore;

        public async Task<HttpResponseMessage> Execute(HttpRequestMessage request, string messageId)
        {
            messageId = messageId?.Replace("/", @"\");

            var result = await dataStore.GetMessageBody(messageId).ConfigureAwait(false);

            if (result.Found == false)
            {
                return request.CreateResponse(HttpStatusCode.NotFound);
            }

            if (result.HasContent == false)
            {
                return request.CreateResponse(HttpStatusCode.NoContent);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);

            HttpContent content;
            if (result.StringContent != null)
            {
                content = new StringContent(result.StringContent);
            }
            else if (result.StreamContent != null)
            {
                content = new StreamContent(result.StreamContent);
            }
            else
            {
                // TODO: What do we do here
                throw new Exception("We should never get here");
            }

            MediaTypeHeaderValue.TryParse(result.ContentType, out var parsedContentType);
            content.Headers.ContentType = parsedContentType ?? new MediaTypeHeaderValue("text/*");

            content.Headers.ContentLength = result.ContentLength;
            response.Headers.ETag = new EntityTagHeaderValue($"\"{result.ETag}\"");
            response.Content = content;

            return response;
        }

        IAuditDataStore dataStore;
    }
}