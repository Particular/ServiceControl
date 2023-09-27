namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System.IO;
    using System.Runtime.Remoting.Contexts;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations.Attachments;
    using ServiceControl.Infrastructure;
    using Sparrow.Json.Parsing;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        const string AttachmentName = "body";
        readonly IDocumentStore documentStore;

        public RavenAttachmentsBodyStorage(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        // TODO: This method is only used in tests and not by ServiceControl itself! But in the Raven3.5 persister, it IS used!
        // It should probably be removed and tests should use the RavenDbRecoverabilityIngestionUnitOfWork
        public async Task Store(string messageId, string contentType, int bodySize, Stream bodyStream)
        {
            var documentId = MessageBodyIdGenerator.MakeDocumentId(messageId);

            var emptyDoc = new DynamicJsonValue();
            var putOwnerDocumentCmd = new PutCommandData(documentId, null, emptyDoc);

            var stream = bodyStream;
            var putAttachmentCmd = new PutAttachmentCommandData(documentId, "body", stream, contentType, changeVector: null);

            using var session = documentStore.OpenAsyncSession();
            session.Advanced.Defer(new ICommandData[] { putOwnerDocumentCmd, putAttachmentCmd });
            await session.SaveChangesAsync();
        }

        public async Task<MessageBodyStreamResult> TryFetch(string messageId)
        {
            var documentId = MessageBodyIdGenerator.MakeDocumentId(messageId);

            using var session = documentStore.OpenAsyncSession();

            var result = await session.Advanced.Attachments.GetAsync(documentId, AttachmentName);

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