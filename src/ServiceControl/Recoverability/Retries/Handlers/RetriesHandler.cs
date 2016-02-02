namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class RetriesHandler : IHandleMessages<RequestRetryAll>,
        IHandleMessages<RetryMessagesById>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<MessageFailedRepeatedly>
    {
        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        public Task Handle(RequestRetryAll message, IMessageHandlerContext context)
        {
            if (message.Endpoint != null)
            {
                Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(m => m.ReceivingEndpointName == message.Endpoint, "all messages for endpoint " + message.Endpoint);
            }
            else
            {
                Retries.StartRetryForIndex<FailedMessage, FailedMessageViewIndex>(context: "all messages");
            }

            return Task.FromResult(0);
        }

        public Task Handle(RetryMessagesById message, IMessageHandlerContext context)
        {
            Retries.StageRetryByUniqueMessageIds(message.MessageUniqueIds);
            return Task.FromResult(0);
        }

        public Task Handle(RetryMessage message, IMessageHandlerContext context)
        {
            Retries.StageRetryByUniqueMessageIds(new [] { message.FailedMessageId });
            return Task.FromResult(0);
        }

        public Task Handle(MessageFailedRepeatedly message, IMessageHandlerContext context)
        {
            RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            return Task.FromResult(0);
        }
    }
}