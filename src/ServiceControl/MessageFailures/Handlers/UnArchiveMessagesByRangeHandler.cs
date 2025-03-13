namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Persistence;

    class UnArchiveMessagesByRangeHandler : IHandleMessages<UnArchiveMessagesByRange>
    {
        public UnArchiveMessagesByRangeHandler(IErrorMessageDataStore dataStore, IDomainEvents domainEvents)
        {
            this.dataStore = dataStore;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(UnArchiveMessagesByRange message, IMessageHandlerContext context)
        {
            var ids = await dataStore.UnArchiveMessagesByRange(message.From, message.To);

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                DocumentIds = ids,
                MessagesCount = ids.Length
            }, context.CancellationToken);

        }

        IErrorMessageDataStore dataStore;
        IDomainEvents domainEvents;

    }
}