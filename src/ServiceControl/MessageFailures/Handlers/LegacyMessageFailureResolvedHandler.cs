namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using Raven.Client;
    using Recoverability;

    /// <summary>
    /// This class handles legacy messages that mark a failed message as successfully retried. For further details go to message definitions.
    /// </summary>
    class LegacyMessageFailureResolvedHandler :
        IHandleMessages<MarkMessageFailureResolvedByRetry>,
        IHandleMessages<MessageFailureResolvedByRetry>
    {
        public LegacyMessageFailureResolvedHandler(IDocumentStore store, IDomainEvents domainEvents, RetryDocumentManager retryDocumentManager)
        {
            this.store = store;
            this.domainEvents = domainEvents;
            this.retryDocumentManager = retryDocumentManager;
        }

        public async Task Handle(MarkMessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            await MarkAsResolvedByRetry(message.FailedMessageId, message.AlternativeFailedMessageIds)
                .ConfigureAwait(false);
            await domainEvents.Raise(new MessageFailureResolvedByRetryDomainEvent
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
            await domainEvents.Raise(new MessageFailureResolvedByRetryDomainEvent
            {
                AlternativeFailedMessageIds = message.AlternativeFailedMessageIds,
                FailedMessageId = message.FailedMessageId
            }).ConfigureAwait(false);
        }

        async Task MarkAsResolvedByRetry(string primaryId, string[] messageAlternativeFailedMessageIds)
        {
            await retryDocumentManager.RemoveFailedMessageRetryDocument(primaryId).ConfigureAwait(false);
            if (await MarkMessageAsResolved(primaryId)
                .ConfigureAwait(false))
            {
                return;
            }

            if (messageAlternativeFailedMessageIds == null)
            {
                return;
            }

            foreach (var alternative in messageAlternativeFailedMessageIds.Where(x => x != primaryId))
            {
                await retryDocumentManager.RemoveFailedMessageRetryDocument(alternative).ConfigureAwait(false);
                if (await MarkMessageAsResolved(alternative)
                    .ConfigureAwait(false))
                {
                    return;
                }
            }
        }

        async Task<bool> MarkMessageAsResolved(string failedMessageId)
        {

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(failedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage == null)
                {
                    return false;
                }

                failedMessage.Status = FailedMessageStatus.Resolved;

                await session.SaveChangesAsync().ConfigureAwait(false);

                return true;
            }
        }

        IDocumentStore store;
        IDomainEvents domainEvents;
        RetryDocumentManager retryDocumentManager;
    }
}