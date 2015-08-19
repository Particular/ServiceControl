namespace ServiceControl.MessageAuditing.Handlers
{
    using Contracts.Operations;
    using Metrics;
    using NServiceBus;
    using Raven.Client;

    class AuditMessageHandler : IHandleMessages<ImportSuccessfullyProcessedMessage>
    {
        static readonly Meter metric = Metric.Meter("Audit message handler", Unit.Items);
        
        public IDocumentSession Session { get; set; }

        public void Handle(ImportSuccessfullyProcessedMessage message)
        {
            var auditMessage = new ProcessedMessage(message);

            Session.Store(auditMessage);

            metric.Mark();
        }

    }
}
