namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client.Documents;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        const string AttachmentName = "body";
        readonly IDocumentStore documentStore;

        public RavenAttachmentsBodyStorage(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public Task Store(string uniqueId, string contentType, int bodySize, Stream bodyStream)
            => throw new NotImplementedException("Only included for interface compatibility with Raven3.5 persister implementation. Raven5 tests should use IIngestionUnitOfWorkFactory to store failed messages/bodies.");

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