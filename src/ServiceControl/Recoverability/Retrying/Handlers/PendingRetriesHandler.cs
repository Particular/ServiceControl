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
        static string[] fields = { "Id" };

        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly RetryDocumentManager manager;

        public PendingRetriesHandler(IBus bus, IDocumentStore store, RetryDocumentManager manager)
        {
            this.bus = bus;
            this.store = store;
            this.manager = manager;
        }

        public void Handle(RetryPendingMessagesById message)
        {
            foreach (var messageUniqueId in message.MessageUniqueIds)
            {
                manager.RemoveFailedMessageRetryDocument(messageUniqueId);
            }

            bus.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = message.MessageUniqueIds);
        }

        public void Handle(RetryPendingMessages message)
        {
            var messageIds = new List<string>();

            using (var session = store.OpenSession())
            {
                var query = session.Advanced
                    .DocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .WhereEquals("Status", (int) FailedMessageStatus.RetryIssued)
                    .AndAlso()
                    .WhereBetweenOrEqual(options => options.LastModified, message.PeriodFrom.Ticks, message.PeriodTo.Ticks)
                    .AndAlso()
                    .WhereEquals(o => o.QueueAddress, message.QueueAddress)
                    .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                    .SelectFields<FailedMessageView>(fields);

                using (var ie = session.Advanced.Stream(query))
                {
                    while (ie.MoveNext())
                    {
                        manager.RemoveFailedMessageRetryDocument(ie.Current.Document.Id);
                        messageIds.Add(ie.Current.Document.Id);
                    }
                }
            }

            bus.SendLocal(new RetryMessagesById {MessageUniqueIds = messageIds.ToArray()});
        }
    }
}
