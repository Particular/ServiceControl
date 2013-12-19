namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.Operations;
    using InternalMessages;
    using NServiceBus;

    public class ImportSuccessfullyProcessedMessageHandler : IHandleMessages<ImportSuccessfullyProcessedMessage>
    {
        public IBus Bus { get; set; }

        public void Handle(ImportSuccessfullyProcessedMessage message)
        {
            string retryId;

            if (!message.PhysicalMessage.Headers.TryGetValue("ServiceControl.RetryId", out retryId))
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