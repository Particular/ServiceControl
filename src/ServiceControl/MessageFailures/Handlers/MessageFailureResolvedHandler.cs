namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Api;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client.Documents;

    class MessageFailureResolvedHandler :
        IHandleMessages<MarkMessageFailureResolvedByRetry>,
        IHandleMessages<MarkPendingRetryAsResolved>,
        IHandleMessages<MarkPendingRetriesAsResolved>
    {
        public MessageFailureResolvedHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public Task Handle(MarkMessageFailureResolvedByRetry message, IMessageHandlerContext context)
        {
            return domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                AlternativeFailedMessageIds = message.AlternativeFailedMessageIds,
                FailedMessageId = message.FailedMessageId
            });
        }

        public async Task Handle(MarkPendingRetriesAsResolved message, IMessageHandlerContext context)
        {
            using (var session = store.OpenAsyncSession())
            {
                var prequery = session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .WhereEquals("Status", FailedMessageStatus.RetryIssued)
                    .AndAlso()
                    .WhereBetween("LastModified", message.PeriodFrom.Ticks, message.PeriodTo.Ticks);

                if (!string.IsNullOrWhiteSpace(message.QueueAddress))
                {
                    prequery = prequery.AndAlso()
                        .WhereEquals(options => options.QueueAddress, message.QueueAddress);
                }

                var query = prequery
                        //TODO:RAVEN5 missing transformenrs and such.
                    //.SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>();

                var ie = await session.Advanced.StreamAsync(query).ConfigureAwait(false);
                while (await ie.MoveNextAsync().ConfigureAwait(false))
                {
                    await context.SendLocal<MarkPendingRetryAsResolved>(m => m.FailedMessageId = ie.Current.Document.Id)
                        .ConfigureAwait(false);
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

        async Task MarkMessageAsResolved(string failedMessageId)
        {
            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(failedMessageId))
                    .ConfigureAwait(false);

                if (failedMessage == null)
                {
                    return;
                }

                failedMessage.Status = FailedMessageStatus.Resolved;

                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        IDocumentStore store;

        IDomainEvents domainEvents;
    }
}