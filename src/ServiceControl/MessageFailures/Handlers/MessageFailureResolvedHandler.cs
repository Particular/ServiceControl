namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Api;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;
    using Recoverability;

    class MessageFailureResolvedHandler :
        IHandleMessages<MarkMessageFailureResolvedByRetry>,
        IHandleMessages<MessageFailureResolvedByRetry>,
        IHandleMessages<MarkPendingRetryAsResolved>,
        IHandleMessages<MarkPendingRetriesAsResolved>
    {
        public MessageFailureResolvedHandler(IDocumentStore store, IDomainEvents domainEvents, RetryDocumentManager retryDocumentManager)
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

        public async Task Handle(MarkPendingRetriesAsResolved message, IMessageHandlerContext context)
        {
            using (var session = store.OpenAsyncSession())
            {
                var prequery = session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .WhereEquals("Status", (int)FailedMessageStatus.RetryIssued)
                    .AndAlso()
                    .WhereBetweenOrEqual("LastModified", message.PeriodFrom.Ticks, message.PeriodTo.Ticks);

                if (!string.IsNullOrWhiteSpace(message.QueueAddress))
                {
                    prequery = prequery.AndAlso()
                        .WhereEquals(options => options.QueueAddress, message.QueueAddress);
                }

                var query = prequery
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>();

                using (var ie = await session.Advanced.StreamAsync(query).ConfigureAwait(false))
                {
                    while (await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        await context.SendLocal<MarkPendingRetryAsResolved>(m => m.FailedMessageId = ie.Current.Document.Id)
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task Handle(MarkPendingRetryAsResolved message, IMessageHandlerContext context)
        {
            await MarkMessageAsResolved(message.FailedMessageId)
                .ConfigureAwait(false);

            await domainEvents.Raise(new MessageFailureResolvedManually
            {
                FailedMessageId = message.FailedMessageId
            }).ConfigureAwait(false);
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