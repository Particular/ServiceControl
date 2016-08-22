namespace ServiceControl.MessageAuditing.Handlers
{
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    class AuditMessageHandler : IHandleMessages<ImportSuccessfullyProcessedMessage>
    {
        private readonly IDocumentStore store;

        public AuditMessageHandler(IDocumentStore store)
        {
            this.store = store;
        }

        public void Handle(ImportSuccessfullyProcessedMessage message)
        {
            var auditMessage = new ProcessedMessage(message);

            using (var session = store.OpenSession())
            {
                session.Store(auditMessage);
                session.SaveChanges();
            }
        }

    }
}
