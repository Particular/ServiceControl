namespace ServiceControl.MessageFailures.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
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
            var (ids, count) = await dataStore.UnArchiveMessagesByRange(
                message.From,
                message.To,
                message.CutOff
                )
                .ConfigureAwait(false);

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                DocumentIds = ids,
                MessagesCount = count
            })
                .ConfigureAwait(false);

        }

        IErrorMessageDataStore dataStore;
        IDomainEvents domainEvents;

    }
}