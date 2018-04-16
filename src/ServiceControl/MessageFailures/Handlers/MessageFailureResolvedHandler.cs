﻿namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Linq;
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
            if (MarkMessageAsResolved(message.FailedMessageId))
            {
                return;
            }

            if (message.AlternativeFailedMessageIds == null)
            {
                return;
            }

            foreach (var alternative in message.AlternativeFailedMessageIds.Where(x => x != message.FailedMessageId))
            {
                if (MarkMessageAsResolved(alternative))
                {
                    return;
                }
            }
        }

        public void Handle(MarkPendingRetryAsResolved message)
        {
            MarkMessageAsResolved(message.FailedMessageId);
            domainEvents.Raise(new MessageFailureResolvedManually
            {
                FailedMessageId = message.FailedMessageId
            });
        }

        public void Handle(MarkPendingRetriesAsResolved message)
        {
            using (var session = store.OpenSession())
            {
                var prequery = session.Advanced
                    .DocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
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

        private bool MarkMessageAsResolved(string failedMessageId)
        {
            using (var session = store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var failedMessage = session.Load<FailedMessage>(new Guid(failedMessageId));

                if (failedMessage == null)
                {
                    return false;
                }

                failedMessage.Status = FailedMessageStatus.Resolved;

                session.SaveChanges();

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
