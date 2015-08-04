namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.InternalMessages;

    // This Handler only exists for messages which are in transit when SC is upgraded to use the new Retries facility
    // Once these messages have been cleared out it is no longer required
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