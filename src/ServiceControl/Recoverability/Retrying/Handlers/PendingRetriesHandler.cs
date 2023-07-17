namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Persistence;

    class PendingRetriesHandler : IHandleMessages<RetryPendingMessagesById>,
        IHandleMessages<RetryPendingMessages>
    {
        public PendingRetriesHandler(IErrorMessageDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        public async Task Handle(RetryPendingMessages message, IMessageHandlerContext context)
        {
            var messageIds = new List<string>();

            var ids = await dataStore.GetRetryPendingMessages(message.PeriodFrom, message.PeriodTo, message.QueueAddress)
                .ConfigureAwait(false);

            foreach (var id in ids)
            {
                await dataStore.RemoveFailedMessageRetryDocument(id)
                    .ConfigureAwait(false);
                messageIds.Add(id);
            }

            await context.SendLocal(new RetryMessagesById { MessageUniqueIds = messageIds.ToArray() })
                .ConfigureAwait(false);
        }

        public async Task Handle(RetryPendingMessagesById message, IMessageHandlerContext context)
        {
            foreach (var messageUniqueId in message.MessageUniqueIds)
            {
                await dataStore.RemoveFailedMessageRetryDocument(messageUniqueId)
                    .ConfigureAwait(false);
            }

            await context.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = message.MessageUniqueIds)
                .ConfigureAwait(false);
        }

        readonly IErrorMessageDataStore dataStore;
    }
}