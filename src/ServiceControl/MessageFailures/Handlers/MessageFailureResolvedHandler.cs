﻿namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Threading.Tasks;
    using Api;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client;

    class MessageFailureResolvedHandler :
        IHandleMessages<MarkPendingRetryAsResolved>,
        IHandleMessages<MarkPendingRetriesAsResolved>
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
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        // In AzureServiceBus transport there is a limit of 100 messages being sent in a single transaction
                        // These do not need to be transactionally consistent so we can dispatch the messages immediately
                        sendOptions.RequireImmediateDispatch();
                        await context.Send<MarkPendingRetryAsResolved>(m => m.FailedMessageId = ie.Current.Document.Id, sendOptions)
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

        async Task MarkMessageAsResolved(string failedMessageId)
        {

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = await session.LoadAsync<FailedMessage>(new Guid(failedMessageId))
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