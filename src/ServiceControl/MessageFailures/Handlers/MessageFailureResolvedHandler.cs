namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Persistence;

    [Handler]
    class MessageFailureResolvedHandler(IFailedMessageRetryDataStore retryStore, IFailedMessageLifecycleDataStore lifecycleStore, IDomainEvents domainEvents) :
        IHandleMessages<MarkPendingRetryAsResolved>,
        IHandleMessages<MarkPendingRetriesAsResolved>
    {
        public Task Handle(MarkPendingRetriesAsResolved message, IMessageHandlerContext context)
        {
            Task ProcessCallback(string id, CancellationToken cancellationToken)
            {
                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                // In AzureServiceBus transport there is a limit of 100 messages being sent in a single transaction
                // These do not need to be transactionally consistent so we can dispatch the messages immediately
                sendOptions.RequireImmediateDispatch();
                return context.Send<MarkPendingRetryAsResolved>(m => m.FailedMessageId = id, sendOptions);
            }

            return retryStore.ProcessPendingRetries(
                message.PeriodFrom,
                message.PeriodTo,
                message.QueueAddress,
                ProcessCallback,
                context.CancellationToken
            );
        }

        public async Task Handle(MarkPendingRetryAsResolved message, IMessageHandlerContext context)
        {
            _ = await lifecycleStore.MarkAsResolved(message.FailedMessageId, context.CancellationToken);

            await domainEvents.Raise(new MessageFailureResolvedManually
            {
                FailedMessageId = message.FailedMessageId
            }, context.CancellationToken);
        }
    }
}