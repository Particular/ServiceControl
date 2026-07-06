namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Persistence;

    [Handler]
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

            await SendRetryMessagesById(context, messageIds.ToArray());
        }

        public async Task Handle(RetryPendingMessagesById message, IMessageHandlerContext context)
        {
            foreach (var messageUniqueId in message.MessageUniqueIds)
            {
                await dataStore.RemoveFailedMessageRetryDocument(messageUniqueId);
            }

            await SendRetryMessagesById(context, message.MessageUniqueIds);
        }

        // The per-message audit entries are emitted at staging time (RetryProcessor.AuditStagedMessages),
        // once a message is really retried — a message resolved here may still never be staged. The audit
        // headers are re-stamped on the follow-up command so the staged batch carries the attribution.
        static Task SendRetryMessagesById(IMessageHandlerContext context, string[] messageUniqueIds)
        {
            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);

            var sendOptions = new SendOptions();
            sendOptions.RouteToThisEndpoint();
            AuditHeaders.Stamp(sendOptions, user, operationId);

            return context.Send(new RetryMessagesById { MessageUniqueIds = messageUniqueIds }, sendOptions);
        }

        readonly IErrorMessageDataStore dataStore;
    }
}
