namespace ServiceControl.MessageFailures
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Operations;

    public class DetectSuccessfullRetriesEnricher : ImportEnricher
    {
        public IBus Bus { get; set; }

        public override void Enrich(ImportMessage message)
        {
            if (!(message is ImportSuccessfullyProcessedMessage))
            {
                return;
            }

            string oldRetryId;
            string newRetryMessageId;
            
            var isOldRetry = message.PhysicalMessage.Headers.TryGetValue("ServiceControl.RetryId", out oldRetryId);
            var isNewRetry = message.PhysicalMessage.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out newRetryMessageId);
            
            var hasBeenRetried = isOldRetry || isNewRetry;
                
            message.Metadata.Add("IsRetried", hasBeenRetried);
            
            if (!hasBeenRetried)
            {
                return;
            }

            if (isOldRetry)
            {
                Bus.Publish<MessageFailureResolvedByRetry>(m => m.FailedMessageId = message.UniqueMessageId);
            }

            if (isNewRetry)
            {
                Bus.Publish<MessageFailureResolvedByRetry>(m => m.FailedMessageId = newRetryMessageId);
            }
        }
    }
}