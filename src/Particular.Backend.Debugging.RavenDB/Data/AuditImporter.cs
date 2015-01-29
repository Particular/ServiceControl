namespace Particular.Backend.Debugging.RavenDB.Data
{
    using Raven.Client;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Operations;

    class AuditImporter : IHandleAuditMessages
    {
        readonly IDocumentStore documentStore;

        public AuditImporter(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public void Handle(ImportSuccessfullyProcessedMessage successfulMessage)
        {
            using (var session = documentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var documentId = ProdDebugMessage.MakeDocumentId(successfulMessage.UniqueMessageId);

                var message = session.Load<ProdDebugMessage>(documentId) ?? new ProdDebugMessage();
                message.Update(successfulMessage);

                session.Store(message);

                session.SaveChanges();
            }
        }
    }
}