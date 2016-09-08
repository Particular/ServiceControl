namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class PendingRetriesHandler : IHandleMessages<RetryPendingMessagesById>,
        IHandleMessages<RetryPendingMessages>
    {
        public IBus Bus { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }
        public IDocumentSession DocumentSession { get; set; }

        static string[] fields = { "Id" };

        public void Handle(RetryPendingMessagesById message)
        {
            foreach (var messageUniqueId in message.MessageUniqueIds)
            {
                RetryDocumentManager.RemoveFailedMessageRetryDocument(messageUniqueId);
            }

            Bus.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = message.MessageUniqueIds);
        }

        public void Handle(RetryPendingMessages message)
        {
            var query = DocumentSession.Advanced
                .DocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                .WhereEquals("Status", (int) FailedMessageStatus.RetryIssued)
                .AndAlso()
                .WhereBetweenOrEqual(options => options.LastModified, message.PeriodFrom.Ticks, message.PeriodTo.Ticks)
                .AndAlso()
                .WhereEquals(o => o.QueueAddress, message.QueueAddress)
                .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                .SelectFields<FailedMessageView>(fields);

            var messageIds = new List<string>();

            using (var ie = DocumentSession.Advanced.Stream(query))
            {
                while (ie.MoveNext())
                {
                    RetryDocumentManager.RemoveFailedMessageRetryDocument(ie.Current.Document.Id);
                    messageIds.Add(ie.Current.Document.Id);
                }
            }

            Bus.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = messageIds.ToArray());
        }
    }
}