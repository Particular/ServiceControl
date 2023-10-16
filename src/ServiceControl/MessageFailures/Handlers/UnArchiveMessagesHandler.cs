namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Persistence;

    class UnArchiveMessagesHandler : IHandleMessages<UnArchiveMessages>
    {
        public UnArchiveMessagesHandler(IErrorMessageDataStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(UnArchiveMessages messages, IMessageHandlerContext context)
        {
            var ids = await store.UnArchiveMessages(messages.FailedMessageIds);

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                DocumentIds = ids,
                MessagesCount = ids.Length
            });
        }

        IErrorMessageDataStore store;
        IDomainEvents domainEvents;
    }
}