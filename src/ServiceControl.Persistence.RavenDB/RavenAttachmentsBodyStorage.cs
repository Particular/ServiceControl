namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Persistence.Infrastructure;
    using Persistence.RavenDB;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class RavenAttachmentsBodyStorage(IRavenSessionProvider sessionProvider) : IBodyStorage
    {
        public const string AttachmentName = "body";

        public async Task<MessageBodyResult> TryFetch(string bodyId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            // BodyId could be a MessageID or a UniqueID, but if a UniqueID then it will be a DeterministicGuid of MessageID and endpoint name and be Guid-parseable
            // This is preferred, then we know we're getting the correct message body that is attached to the FailedMessage document
            if (Guid.TryParse(bodyId, out _))
            {
                var result = await ResultForUniqueId(session, bodyId, cancellationToken);
                if (result.State != MessageBodyState.NotFound)
                {
                    return result;
                }
            }

            // See if we can look up a FailedMessage by MessageID
            var query = session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .Where(msg => msg.MessageId == bodyId, true)
                .OfType<FailedMessage>()
                .Select(msg => msg.UniqueMessageId);

            var uniqueId = await query.FirstOrDefaultAsync(cancellationToken);

            if (uniqueId != null)
            {
                return await ResultForUniqueId(session, uniqueId, cancellationToken);
            }

            return MessageBodyResult.NotFound();
        }

        async Task<MessageBodyResult> ResultForUniqueId(IAsyncDocumentSession session, string uniqueId, CancellationToken cancellationToken)
        {
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueId);
            var failedMessage = await session.LoadAsync<FailedMessage>(documentId, cancellationToken);

            if (failedMessage == null)
            {
                return MessageBodyResult.NotFound();
            }

            var result = await session.Advanced.Attachments.GetAsync(documentId, AttachmentName, cancellationToken);

            if (result == null)
            {
                return MessageBodyResult.Unavailable();
            }

            if (result.Details.Size == 0)
            {
                await result.Stream.DisposeAsync();
                return MessageBodyResult.Empty();
            }

            return MessageBodyResult.Available(new MessageBodyStreamContent(
                result.Stream,
                result.Details.ContentType,
                (int)result.Details.Size,
                // The change vector moves whenever the stored bytes do.
                DataVersion.FromContent(result.Details.ChangeVector)));
        }
    }
}