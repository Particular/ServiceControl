namespace ServiceControl.MessageAuditing.Handlers
{
    using System.Threading.Tasks;
    using Contracts.Operations;
    using NServiceBus;
    using Raven.Client;

    class AuditMessageHandler : IHandleMessages<ImportSuccessfullyProcessedMessage>
    {
        public IDocumentSession Session { get; set; }

        public Task Handle(ImportSuccessfullyProcessedMessage message, IMessageHandlerContext context)
        {
            var auditMessage = new ProcessedMessage(message);

            Session.Store(auditMessage);
        }
    }
}
