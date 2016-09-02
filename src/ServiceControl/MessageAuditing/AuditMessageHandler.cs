namespace ServiceControl.MessageAuditing.Handlers
{
    using System;
    using Contracts.Operations;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceControl.Operations;

    class AuditMessageHandler
    {
        private readonly IDocumentStore store;
        private readonly IEnrichImportedMessages[] enrichers;

        public AuditMessageHandler(IDocumentStore store, IEnrichImportedMessages[] enrichers)
        {
            this.store = store;
            this.enrichers = enrichers;
        }

        public void Handle(ImportSuccessfullyProcessedMessage message)
        {
            var auditMessage = CreateProcessedMessage(message);

            using (var session = store.OpenSession())
            {
                session.Store(auditMessage);
                session.SaveChanges();
            }
        }

        public void Handle(BulkInsertOperation bulkInsert, ImportSuccessfullyProcessedMessage message)
        {
            var auditMessage = CreateProcessedMessage(message);

            bulkInsert.Store(auditMessage);
        }

        private ProcessedMessage CreateProcessedMessage(ImportSuccessfullyProcessedMessage message)
        {
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(message.PhysicalMessage.Headers, message.Metadata);
            }

            var auditMessage = new ProcessedMessage(message)
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };
            return auditMessage;
        }
    }
}