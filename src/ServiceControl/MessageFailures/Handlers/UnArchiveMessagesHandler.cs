namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Persistence;

    class UnArchiveMessagesHandler(IErrorMessageDataStore store, IDomainEvents domainEvents)
        : IHandleMessages<UnArchiveMessages>
    {
        public async Task Handle(UnArchiveMessages messages, IMessageHandlerContext context)
        {
            var ids = await store.UnArchiveMessages(messages.FailedMessageIds);

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                DocumentIds = ids,
                MessagesCount = ids.Length
            }, context.CancellationToken);
        }
    }
}