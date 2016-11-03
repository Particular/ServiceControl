﻿namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class MessageFailureResolvedHandler : IHandleMessages<MessageFailureResolvedByRetry>, IHandleMessages<MarkPendingRetryAsResolved>, IHandleMessages<MarkPendingRetriesAsResolved>
    {
        private readonly IBus bus;
        private readonly IDocumentStore store;

        public MessageFailureResolvedHandler(IBus bus, IDocumentStore store)
        {
            this.bus = bus;
            this.store = store;
        }

        public void Handle(MessageFailureResolvedByRetry message)
        {
            MarkMessageAsResolved(message.FailedMessageId);
        }

        public void Handle(MarkPendingRetryAsResolved message)
        {
            MarkMessageAsResolved(message.FailedMessageId);
            bus.Publish<MessageFailureResolvedManually>(m => m.FailedMessageId = message.FailedMessageId);
        }

        public void Handle(MarkPendingRetriesAsResolved message)
        {
            using (var session = store.OpenSession())
            {
                var prequery = session.Advanced
                    .LuceneQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
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

                using (var ie = session.Advanced.Stream(query))
                {
                    while (ie.MoveNext())
                    {
                        bus.SendLocal<MarkPendingRetryAsResolved>(m => m.FailedMessageId = ie.Current.Document.Id);
                    }
                }
            }
        }

        private void MarkMessageAsResolved(string failedMessageId)
        {
            using (var session = store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = session.Load<FailedMessage>(new Guid(failedMessageId));

                if (failedMessage == null)
                {
                    return;
                }

                failedMessage.Status = FailedMessageStatus.Resolved;

                session.SaveChanges();
            }
        }
    }
}
