namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using Persistence;

    /// <summary>
    /// This class handles legacy messages that mark a failed message as successfully retried. For further details go to message definitions.
    /// </summary>
    [Handler]
    class LegacyMessageFailureResolvedHandler(IFailedMessageRetryDataStore retryStore, IFailedMessageLifecycleDataStore lifecycleStore, IDomainEvents domainEvents) :
        IHandleMessages<MarkMessageFailureResolvedByRetry>,
        IHandleMessages<MessageFailureResolvedByRetry>
    {
        public async Task Handle(MarkMessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            await MarkAsResolvedByRetry(message.FailedMessageId, message.AlternativeFailedMessageIds);
            await domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                AlternativeFailedMessageIds = message.AlternativeFailedMessageIds,
                FailedMessageId = message.FailedMessageId
            }, context.CancellationToken);
        }

        // This is only needed because we might get this from legacy not yet converted instances
        public async Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            await MarkAsResolvedByRetry(message.FailedMessageId, message.AlternativeFailedMessageIds);
            await domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                AlternativeFailedMessageIds = message.AlternativeFailedMessageIds,
                FailedMessageId = message.FailedMessageId
            }, context.CancellationToken);
        }

        async Task MarkAsResolvedByRetry(string primaryId, string[] messageAlternativeFailedMessageIds)
        {
            await retryStore.RemoveFailedMessageRetry(primaryId);

            var primaryUpdated = await lifecycleStore.MarkAsResolved(primaryId);

            if (primaryUpdated)
            {
                return;
            }

            if (messageAlternativeFailedMessageIds == null)
            {
                return;
            }

            foreach (var alternative in messageAlternativeFailedMessageIds.Where(x => x != primaryId))
            {
                await retryStore.RemoveFailedMessageRetry(alternative);

                var alternativeUpdated = await lifecycleStore.MarkAsResolved(alternative);

                if (alternativeUpdated)
                {
                    return;
                }
            }
        }
    }
}