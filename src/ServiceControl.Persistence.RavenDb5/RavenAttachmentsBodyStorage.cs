namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Operations.Attachments;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        const string AttachmentName = "body";
        readonly IDocumentStore documentStore;

        public RavenAttachmentsBodyStorage(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task Store(string messageId, string contentType, int bodySize, Stream bodyStream)
        {
            // var id = MessageBodyIdGenerator.MakeDocumentId(messageId); // TODO: Not needed? Not used by audit

            using var session = documentStore.OpenAsyncSession();

            // Following is possible to but not documented in the Raven docs.
            //session.Advanced.Attachments.Store(messageId,"body",bodyStream,contentType);
            // https://ravendb.net/docs/article-page/5.4/csharp/client-api/operations/attachments/get-attachment
            _ = await documentStore.Operations.SendAsync(
                    new PutAttachmentOperation(messageId,
                        AttachmentName,
                        bodyStream,
                        contentType));
        }

        public async Task<MessageBodyStreamResult> TryFetch(string messageId)
        {
            //var messageId = MessageBodyIdGenerator.MakeDocumentId(bodyId); // TODO: Not needed? Not used by audit

            using var session = documentStore.OpenAsyncSession();

            var result = await session.Advanced.Attachments.GetAsync(messageId, AttachmentName);

            if (result == null)
            {
                return null;
            }

            return new MessageBodyStreamResult
            {
                HasResult = true,
                Stream = result.Stream,
                ContentType = result.Details.ContentType,
                BodySize = (int)result.Details.Size,
                Etag = result.Details.ChangeVector
            };
        }
    }
}