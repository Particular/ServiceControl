namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;

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
        public async Task Store(string uniqueId, string contentType, int bodySize, Stream bodyStream)
        {
            // In RavenDB 5 persistence, the ID must be the UniqueID representing MessageID+Endpoint so that we can
            // load the body from the FailedMessage/{UniqueId} document.
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueId);

            var stream = bodyStream;
            var putAttachmentCmd = new PutAttachmentCommandData(documentId, "body", stream, contentType, changeVector: null);

            using var session = documentStore.OpenAsyncSession();
            session.Advanced.Defer(putAttachmentCmd);
            await session.SaveChangesAsync();
        }

        public async Task<MessageBodyStreamResult> TryFetch(string uniqueId)
        {
            // In RavenDB 5 persistence, the ID must be the UniqueID representing MessageID+Endpoint so that we can
            // load the body from the FailedMessage/{UniqueId} document.
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueId);

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