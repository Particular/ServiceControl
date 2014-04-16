namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    public class MessageSubmittedForRetryDefinition : EventLogMappingDefinition<MessageSubmittedForRetry>
    {
        public MessageSubmittedForRetryDefinition()
        {
            Description(m => "Failed message submitted for retry");

            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}