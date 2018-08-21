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

    public class MessageFailureResolvedHandler : IHandleMessages<MessageFailureResolvedByRetry>, IHandleMessages<MarkPendingRetryAsResolved>, IHandleMessages<MarkPendingRetriesAsResolved>
    {
        public MessageFailureResolvedHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
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

        public async Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            if (await MarkMessageAsResolved(message.FailedMessageId)
                .ConfigureAwait(false))
            {
                return;
            }

            if (message.AlternativeFailedMessageIds == null)
            {
                return;
            }

            foreach (var alternative in message.AlternativeFailedMessageIds.Where(x => x != message.FailedMessageId))
            {
                if (await MarkMessageAsResolved(alternative)
                    .ConfigureAwait(false))
                {
                    return;
                }
            }
        }

        private async Task<bool> MarkMessageAsResolved(string failedMessageId)
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

        private enum MarkMessageAsResolvedStatus
        {
            NotFound,
            Updated
        }
    }
}