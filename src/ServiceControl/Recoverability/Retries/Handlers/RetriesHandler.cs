namespace ServiceControl.Recoverability
{
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

        public void Handle(RequestRetryAll message)
        {
            if (message.Endpoint != null)
            {
                Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(m => m.ReceivingEndpointName == message.Endpoint, "Retry all for endpoint '" + message.Endpoint + "' batch {0} of {1}");
            }
            else
            {
                Retries.StartRetryForIndex<FailedMessage, FailedMessageViewIndex>(context: "Retry all batch {0} of {1}");
            }
        }

        public void Handle(RetryMessagesById message)
        {
            Retries.StageRetryByUniqueMessageIds(message.MessageUniqueIds);
        }

        public void Handle(RetryMessage message)
        {
            Retries.StageRetryByUniqueMessageIds(new [] { message.FailedMessageId });
        }

        public void Handle(MessageFailedRepeatedly message)
        {
            RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
        }
    }
}