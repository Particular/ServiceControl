namespace ServiceControl.MessageAuditing.Handlers
{
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;

    class AuditMessageHandler : IHandleMessages<ImportSuccessfullyProcessedMessage>
    {
        public IDocumentStore Store { get; set; }
        
        public void Handle(ImportSuccessfullyProcessedMessage message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var auditMessage = new ProcessedMessage(message);


                try
                {
                    session.Store(auditMessage);
                    session.SaveChanges();
                }
                catch (ConcurrencyException)
                {
                    //already stored
                }
            }
        }

    }
}
