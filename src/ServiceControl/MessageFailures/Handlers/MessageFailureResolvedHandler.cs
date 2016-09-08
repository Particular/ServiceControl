namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class MessageFailureResolvedHandler : IHandleMessages<MessageFailureResolvedByRetry>, IHandleMessages<MarkPendingRetryAsResolved>, IHandleMessages<MarkPendingRetriesAsResolved>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(MessageFailureResolvedByRetry message)
        {
            MarkMessageAsResolved(message.FailedMessageId);
        }

        public void Handle(MarkPendingRetryAsResolved message)
        {
            MarkMessageAsResolved(message.FailedMessageId);
            Bus.Publish<MessageFailureResolvedManually>(m => m.FailedMessageId = message.FailedMessageId);
        }

        public void Handle(MarkPendingRetriesAsResolved message)
        {
            var prequery = Session.Advanced
                .DocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
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

            using (var ie = Session.Advanced.Stream(query))
            {
                while (ie.MoveNext())
                {
                    Bus.SendLocal<MarkPendingRetryAsResolved>(m => m.FailedMessageId = ie.Current.Document.Id);
                }
            }
        }

        private void MarkMessageAsResolved(string failedMessageId)
        {
            var failedMessage = Session.Load<FailedMessage>(new Guid(failedMessageId));

            if (failedMessage == null)
            {
                return;
            }

            failedMessage.Status = FailedMessageStatus.Resolved;
        }
    }
}