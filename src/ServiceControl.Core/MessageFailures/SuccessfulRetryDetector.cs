namespace ServiceControl.MessageFailures
{
    using System;
    using InternalMessages;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.MessageFailures;

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
            string uniqueMessageId;
            hasBeenRetried |= message.Headers.TryGet("ServiceControl.Retry.UniqueMessageId", out uniqueMessageId);

            if (!hasBeenRetried)
            {
                return;
            }

            if (retryId != null)
            {
                bus.SendLocal(new RegisterSuccessfulRetry
                {
                    FailedMessageId = message.UniqueId,
                    RetryId = Guid.Parse(retryId),
                    FailedMessageType = message.MessageType.Name,
                });
            }

            if (uniqueMessageId != null)
            {
                bus.Publish<MessageFailureResolvedByRetry>(m =>
                {
                    m.FailedMessageId = uniqueMessageId;
                    m.FailedMessageType = message.MessageType.Name;
                });
            }
        }
    }
}