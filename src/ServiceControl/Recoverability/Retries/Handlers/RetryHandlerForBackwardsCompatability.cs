namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;

    public class RetryHandlerForBackwardsCompatability : IHandleMessages<RegisterSuccessfulRetry>, IHandleMessages<PerformRetry>
    {
        public void Handle(RegisterSuccessfulRetry message)
        {
            Bus.Publish<MessageFailureResolvedByRetry>(m =>
            {
                m.FailedMessageId = message.FailedMessageId;
            });
        }

        public void Handle(PerformRetry message)
        {
            Bus.Publish<RetryMessagesById>(m => m.MessageUniqueIds = new [] { message.FailedMessageId });
        }

        public IBus Bus { get; set; }
    }
}