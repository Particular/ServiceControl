namespace ServiceControl.MessageFailures
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;
    using ServiceControl.Recoverability.Retries;

    public class ImportFailedMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        readonly IBus bus;
        readonly RetryDocumentManager retryDocumentManager;

        public ImportFailedMessageHandler(IBus bus, RetryDocumentManager retryDocumentManager)
        {
            this.bus = bus;
            this.retryDocumentManager = retryDocumentManager;
        }

        public void Handle(ImportFailedMessage message)
        {
            var failedMessageId = message.GetHeader("ServiceControl.Retry.UniqueMessageId");

            if (failedMessageId != null)
            {
                bus.Publish<MessageFailedRepeatedly>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointName;
                    m.FailedMessageId = failedMessageId;
                });
                retryDocumentManager.RemoveFailureRetryDocument(failedMessageId);
            }
            else
            {
                bus.Publish<MessageFailed>(m =>
                {
                    m.FailureDetails = message.FailureDetails;
                    m.EndpointId = message.FailingEndpointName;
                    m.FailedMessageId = message.UniqueMessageId;
                });
            }
        }
    }
}