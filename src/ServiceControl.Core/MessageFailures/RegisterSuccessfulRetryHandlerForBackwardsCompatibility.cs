namespace ServiceControl.MessageFailures
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;

    public class RegisterSuccessfulRetryHandlerForBackwardsCompatibility : IHandleMessages<RegisterSuccessfulRetry>
    {
        readonly IBus bus;

        public RegisterSuccessfulRetryHandlerForBackwardsCompatibility(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(RegisterSuccessfulRetry message)
        {
            bus.Publish<MessageFailureResolvedByRetry>(m =>
            {
                m.FailedMessageId = message.FailedMessageId;
                m.FailedMessageType = message.FailedMessageType;
            });
        }
    }
}