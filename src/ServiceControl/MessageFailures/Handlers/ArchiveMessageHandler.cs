namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using ServiceControl.Persistence;

    class ArchiveMessageHandler : IHandleMessages<ArchiveMessage>
    {
        public ArchiveMessageHandler(IErrorMessageDataStore dataStore, IDomainEvents domainEvents)
        {
            this.dataStore = dataStore;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(ArchiveMessage message, IMessageHandlerContext context)
        {
            var failedMessageId = message.FailedMessageId;

            var failedMessage = await dataStore.ErrorBy(failedMessageId);

            if (failedMessage.Status != FailedMessageStatus.Archived)
            {
                await domainEvents.Raise(new FailedMessageArchived
                {
                    FailedMessageId = failedMessageId
                }, context.CancellationToken);

                await dataStore.FailedMessageMarkAsArchived(failedMessageId);
            }
        }

        IErrorMessageDataStore dataStore;
        IDomainEvents domainEvents;
    }
}