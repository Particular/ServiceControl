namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using MessageFailures.Api;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Raven.Client.Documents;

    class PendingRetriesHandler : IHandleMessages<RetryPendingMessagesById>,
        IHandleMessages<RetryPendingMessages>
    {
        public PendingRetriesHandler(IDocumentStore store, RetryDocumentManager manager)
        {
            this.store = store;
            this.manager = manager;
        }

        public async Task Handle(RetryPendingMessages message, IMessageHandlerContext context)
        {
            var messageIds = new List<string>();

            using (var session = store.OpenAsyncSession())
            {
                var query = session.Advanced
                    .AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .WhereEquals("Status", (int)FailedMessageStatus.RetryIssued)
                    .AndAlso()
                    .WhereBetween(options => options.LastModified, message.PeriodFrom.Ticks, message.PeriodTo.Ticks)
                    .AndAlso()
                    .WhereEquals(o => o.QueueAddress, message.QueueAddress)
                    //TODO:RAVEN5 missing API transformers and such
                    //.SetResultTransformer(FailedMessageViewTransformer.Name)
                    .SelectFields<FailedMessageView>(fields);

                var ie = await session.Advanced.StreamAsync(query).ConfigureAwait(false);

                while (await ie.MoveNextAsync().ConfigureAwait(false))
                {
                    var documentId = ie.Current.Document.Id.Replace("FailedMessages/", "");

                    await manager.RemoveFailedMessageRetryDocument(documentId)
                        .ConfigureAwait(false);
                    messageIds.Add(documentId);
                }

            }

            await context.SendLocal(new RetryMessagesById {MessageUniqueIds = messageIds.ToArray()})
                .ConfigureAwait(false);
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

        private readonly IDocumentStore store;
        private readonly RetryDocumentManager manager;
        static string[] fields = {"Id"};
    }
}