namespace ServiceControl.MessageFailures
{
    using System;
    using InternalMessages;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;

    public class SuccessfulRetryDetector : IProcessSuccessfulMessages
    {
        readonly IBus bus;

        public SuccessfulRetryDetector(IBus bus)
        {
            this.bus = bus;
        }

        public void ProcessSuccessful(IngestedMessage message)
        {
            string retryId;
            var hasBeenRetried = message.Headers.TryGet("ServiceControl.RetryId", out retryId);
            if (!hasBeenRetried)
            {
                return;
            }

            bus.SendLocal(new RegisterSuccessfulRetry
            {
                FailedMessageId = message.UniqueId,
                RetryId = Guid.Parse(retryId)
            });
        }
    }
}