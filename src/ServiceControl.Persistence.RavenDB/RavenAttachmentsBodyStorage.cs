namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Persistence.RavenDB;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class RavenAttachmentsBodyStorage(IRavenSessionProvider sessionProvider) : IBodyStorage
    {
        public const string AttachmentName = "body";

        public async Task<MessageBodyStreamResult> TryFetch(string bodyId)
        {
            using var session = await sessionProvider.OpenSession();

            // BodyId could be a MessageID or a UniqueID, but if a UniqueID then it will be a DeterministicGuid of MessageID and endpoint name and be Guid-parseable
            // This is preferred, then we know we're getting the correct message body that is attached to the FailedMessage document
            if (Guid.TryParse(bodyId, out _))
            {
                var result = await ResultForUniqueId(session, bodyId);
                if (result != null)
                {
                    return result;
                }
            }

            // See if we can look up a FailedMessage by MessageID
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(msg => msg.MessageId == bodyId, true)
                .OfType<FailedMessage>()
                .Select(msg => msg.UniqueMessageId);

            var uniqueId = await query.FirstOrDefaultAsync();

            if (uniqueId != null)
            {
                return await ResultForUniqueId(session, uniqueId);
            }

            return null;
        }

        async Task<MessageBodyStreamResult> ResultForUniqueId(IAsyncDocumentSession session, string uniqueId)
        {
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueId);

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