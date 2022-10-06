namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using System.Threading.Tasks;
    using Auditing;
    using Infrastructure;
    using Monitoring;
    using NServiceBus;
    using Persistence.UnitOfWork;
    using Raven.Client;
    using Raven.Client.Documents.Session;
    using SagaAudit;

    class SessionBasedRavenDbAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        public SessionBasedRavenDbAuditIngestionUnitOfWork(IAsyncDocumentSession session,
            TimeSpan auditRetentionPeriod, int maxBodySizeToStore)
        {
            this.session = session;
            this.auditRetentionPeriod = auditRetentionPeriod;
            this.maxBodySizeToStore = maxBodySizeToStore;
        }

        public async ValueTask DisposeAsync()
        {
            await session.SaveChangesAsync().ConfigureAwait(false);
            session.Dispose();
        }

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, byte[] body = null)
        {
            if (body != null)
            {
                // TODO: Pull both of these up to the higher level
                processedMessage.MessageMetadata["ContentLength"] = body.Length;
                processedMessage.MessageMetadata["BodyUrl"] = $"/messages/{processedMessage.Id}/body";
            }
            else
            {
                // TODO: Tests were designed for 3.5 where attachments were a separate thing
                // so tests can generate a processed message with no body. But there is no use
                // case for that outside of tests.
                processedMessage.MessageMetadata["ContentLength"] = 0;
            }

            await session.StoreAsync(processedMessage).ConfigureAwait(false);
            SetExpiry(processedMessage);

            if (body != null && body.Length <= maxBodySizeToStore)
            {
                using (var stream = Memory.Manager.GetStream(Guid.NewGuid(), processedMessage.Id, body, 0, body.Length))
                {
                    if (!processedMessage.Headers.TryGetValue(Headers.ContentType, out var contentType))
                    {
                        // TODO: is there a use case for no content type? and if that's the case is it expected that the default is text/xml?
                        contentType = "text/xml";
                    }

                    session.Advanced.Attachments.Store(processedMessage, "body", stream, contentType);
                }
            }
        }

        void SetExpiry<T>(T item)
            => session.Advanced.GetMetadataFor(item)[Constants.Documents.Metadata.Expires]
                = DateTime.UtcNow.Add(auditRetentionPeriod);

        public async Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot)
        {
            await session.StoreAsync(sagaSnapshot).ConfigureAwait(false);
            SetExpiry(sagaSnapshot);
        }

        public async Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
        {
            await session.StoreAsync(knownEndpoint).ConfigureAwait(false);
            SetExpiry(knownEndpoint);
        }

        readonly IAsyncDocumentSession session;
        readonly TimeSpan auditRetentionPeriod;
        readonly int maxBodySizeToStore;
    }
}