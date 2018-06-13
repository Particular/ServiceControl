namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class PendingRetriesHandler : IHandleMessages<RetryPendingMessagesById>,
        IHandleMessages<RetryPendingMessages>
    {
        static string[] fields = { "Id" };

        private readonly IDocumentStore store;
        private readonly RetryDocumentManager manager;

        public PendingRetriesHandler(IDocumentStore store, RetryDocumentManager manager)
        {
            this.store = store;
            this.manager = manager;
        }

        public async Task Handle(RetryPendingMessagesById message, IMessageHandlerContext context)
        {
            foreach (var messageUniqueId in message.MessageUniqueIds)
            {
                await manager.RemoveFailedMessageRetryDocument(messageUniqueId)
                    .ConfigureAwait(false);
            }

            await context.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = message.MessageUniqueIds)
                .ConfigureAwait(false);
        }
        
        public async Task Handle(RetryPendingMessages message, IMessageHandlerContext context)
        {
            var messageIds = new List<string>();

            using (var session = store.OpenAsyncSession())
            {
                var query = session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .WhereEquals("Status", (int) FailedMessageStatus.RetryIssued)
                    .AndAlso()
                    .WhereBetweenOrEqual(options => options.LastModified, message.PeriodFrom.Ticks, message.PeriodTo.Ticks)
                    .AndAlso()
                    .WhereEquals(o => o.QueueAddress, message.QueueAddress)
                    .SetResultTransformer(FailedMessageViewTransformer.Name)
                    .SelectFields<FailedMessageView>(fields);

                using (var ie = await session.Advanced.StreamAsync(query).ConfigureAwait(false))
                {
                    while (await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        await manager.RemoveFailedMessageRetryDocument(ie.Current.Document.Id)
                            .ConfigureAwait(false);
                        messageIds.Add(ie.Current.Document.Id);
                    }
                }
            }

            await context.SendLocal(new RetryMessagesById {MessageUniqueIds = messageIds.ToArray()})
                .ConfigureAwait(false);
        }
    }
}
