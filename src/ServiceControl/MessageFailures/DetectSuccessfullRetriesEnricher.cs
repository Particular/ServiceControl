namespace ServiceControl.MessageFailures
{
    using System;
    using Contracts.Operations;
    using InternalMessages;
    using NServiceBus;
    using Operations;

    public class DetectSuccessfullRetriesEnricher : ImportEnricher
    {
        public IBus Bus { get; set; }

        public override void Enrich(ImportMessage message)
        {
            if (!(message is ImportSuccessfullyProcessedMessage))
            {
                return;
            }

            string retryId;
            
            var hasBeenRetried = message.PhysicalMessage.Headers.TryGetValue("ServiceControl.RetryId", out retryId);
                
            message.Metadata.Add("IsRetried", hasBeenRetried);
            
            if (!hasBeenRetried)
            {
                return;
            }

            Bus.SendLocal(new RegisterSuccessfulRetry
            {
                FailedMessageId = message.UniqueMessageId,
                RetryId = Guid.Parse(retryId)
            });
        }
    }
}