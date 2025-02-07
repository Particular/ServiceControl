namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Persistence;

    class MessageFailureResolvedHandler :
        IHandleMessages<MarkPendingRetryAsResolved>,
        IHandleMessages<MarkPendingRetriesAsResolved>
    {
        public MessageFailureResolvedHandler(IErrorMessageDataStore dataStore, IDomainEvents domainEvents)
        {
            this.dataStore = dataStore;
            this.domainEvents = domainEvents;
        }

        public Task Handle(MarkPendingRetriesAsResolved message, IMessageHandlerContext context)
        {
            Task ProcessCallback(string id)
            {
                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                // In AzureServiceBus transport there is a limit of 100 messages being sent in a single transaction
                // These do not need to be transactionally consistent so we can dispatch the messages immediately
                sendOptions.RequireImmediateDispatch();
                return context.Send<MarkPendingRetryAsResolved>(m => m.FailedMessageId = id, sendOptions);
            }

            return dataStore.ProcessPendingRetries(
                message.PeriodFrom,
                message.PeriodTo,
                message.QueueAddress,
                ProcessCallback
            );
        }

        public async Task Handle(MarkPendingRetryAsResolved message, IMessageHandlerContext context)
        {
            _ = await dataStore.MarkMessageAsResolved(message.FailedMessageId);

            await domainEvents.Raise(new MessageFailureResolvedManually
            {
                FailedMessageId = message.FailedMessageId
            }, context.CancellationToken);
        }

        IErrorMessageDataStore dataStore;
        IDomainEvents domainEvents;
    }
}