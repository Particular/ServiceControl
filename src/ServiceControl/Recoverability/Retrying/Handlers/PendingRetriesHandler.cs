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

            var ids = await dataStore.GetRetryPendingMessages(message.PeriodFrom, message.PeriodTo, message.QueueAddress);

            foreach (var id in ids)
            {
                await dataStore.RemoveFailedMessageRetryDocument(id);
                messageIds.Add(id);
            }

            await context.SendLocal(new RetryMessagesById { MessageUniqueIds = messageIds.ToArray() });
        }

        public async Task Handle(RetryPendingMessagesById message, IMessageHandlerContext context)
        {
            foreach (var messageUniqueId in message.MessageUniqueIds)
            {
                await dataStore.RemoveFailedMessageRetryDocument(messageUniqueId);
            }

            await context.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = message.MessageUniqueIds);
        }

        readonly IErrorMessageDataStore dataStore;
    }
}