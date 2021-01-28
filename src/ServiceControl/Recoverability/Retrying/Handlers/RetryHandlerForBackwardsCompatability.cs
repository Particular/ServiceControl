namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures.InternalMessages;
    using NServiceBus;

    // This Handler only exists for messages which are in transit when SC is upgraded to use the new Retries facility
    // Once these messages have been cleared out it is no longer required
    class RetryHandlerForBackwardsCompatability : IHandleMessages<RegisterSuccessfulRetry>, IHandleMessages<PerformRetry>
    {
        public Task Handle(PerformRetry message, IMessageHandlerContext context)
        {
            return context.SendLocal<RetryMessagesById>(m =>
                m.MessageUniqueIds = new[] { message.FailedMessageId });
        }

        public Task Handle(RegisterSuccessfulRetry message, IMessageHandlerContext context)
        {
            return context.SendLocal<MarkMessageFailureResolvedByRetry>(m => { m.FailedMessageId = message.FailedMessageId; });
        }
    }
}