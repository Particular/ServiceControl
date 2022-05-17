namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageFailures;
    using ServiceControl.EventLog;

    class MessageSubmittedForRetryDefinition : EventLogMappingDefinition<MessageSubmittedForRetry>
    {
        public MessageSubmittedForRetryDefinition()
        {
            Description(m => "Failed message submitted for retry");

            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}