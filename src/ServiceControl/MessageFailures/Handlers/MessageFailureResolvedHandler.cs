namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class MessageFailureResolvedHandler : IHandleMessages<MessageFailureResolvedByRetry>, IHandleMessages<MarkPendingRetryAsResolved>, IHandleMessages<MarkPendingRetriesAsResolved>
    {
        IBus bus;
        IDocumentStore store;
        IDomainEvents domainEvents;

        public MessageFailureResolvedHandler(IBus bus, IDocumentStore store, IDomainEvents domainEvents)
        {
            this.bus = bus;
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void Handle(MessageFailureResolvedByRetry message)
        {
            HandleAsync(message).GetAwaiter().GetResult();
        }

        private async Task HandleAsync(MessageFailureResolvedByRetry message)
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

        public void Handle(MarkPendingRetryAsResolved message)
        {
            HandleAsync(message).GetAwaiter().GetResult();
        }

        private async Task HandleAsync(MarkPendingRetryAsResolved message)
        {
            await MarkMessageAsResolved(message.FailedMessageId)
                .ConfigureAwait(false);
            await domainEvents.Raise(new MessageFailureResolvedManually
            {
                FailedMessageId = message.FailedMessageId
            }).ConfigureAwait(false);
        }

        public void Handle(MarkPendingRetriesAsResolved message)
        {
            HandleAsync(message).GetAwaiter().GetResult();
        }

        private async Task HandleAsync(MarkPendingRetriesAsResolved message)
        {
            using (var session = store.OpenAsyncSession())
            {
                var prequery = session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .WhereEquals("Status", (int) FailedMessageStatus.RetryIssued)
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
                        bus.SendLocal<MarkPendingRetryAsResolved>(m => m.FailedMessageId = ie.Current.Document.Id);
                    }
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

        private enum MarkMessageAsResolvedStatus
        {
            NotFound,
            Updated
        }
    }
}
