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
    class LegacyMessageFailureResolvedHandler :
        IHandleMessages<MarkMessageFailureResolvedByRetry>,
        IHandleMessages<MessageFailureResolvedByRetry>
    {
        public LegacyMessageFailureResolvedHandler(IErrorMessageDataStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(MarkMessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            await MarkAsResolvedByRetry(message.FailedMessageId, message.AlternativeFailedMessageIds)
                .ConfigureAwait(false);
            await domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                AlternativeFailedMessageIds = message.AlternativeFailedMessageIds,
                FailedMessageId = message.FailedMessageId
            }).ConfigureAwait(false);
        }

        // This is only needed because we might get this from legacy not yet converted instances
        public async Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            await MarkAsResolvedByRetry(message.FailedMessageId, message.AlternativeFailedMessageIds)
                .ConfigureAwait(false);
            await domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                AlternativeFailedMessageIds = message.AlternativeFailedMessageIds,
                FailedMessageId = message.FailedMessageId
            }).ConfigureAwait(false);
        }

        async Task MarkAsResolvedByRetry(string primaryId, string[] messageAlternativeFailedMessageIds)
        {
            await store.RemoveFailedMessageRetryDocument(primaryId).ConfigureAwait(false);

            var primaryUpdated = await store.MarkMessageAsResolved(primaryId).ConfigureAwait(false);

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
                await store.RemoveFailedMessageRetryDocument(alternative).ConfigureAwait(false);

                var alternativeUpdated = await store.MarkMessageAsResolved(alternative).ConfigureAwait(false);

                if (alternativeUpdated)
                {
                    return;
                }
            }
        }

        readonly IErrorMessageDataStore store;
        readonly IDomainEvents domainEvents;
    }
}