namespace ServiceControl.MessageFailures
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;

    public class ImportFailedMessageHandler : IHandleMessages<ImportFailedMessage>
    {
        readonly IBus bus;

        public ImportFailedMessageHandler(IBus bus)
        {
            this.bus = bus;
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

            //TODO: Delete retry batch document
        }
    }
}