namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    public class MessageFailureResolvedByRetryDefinition : EventLogMappingDefinition<MessageFailureResolvedByRetry>
    {
        public MessageFailureResolvedByRetryDefinition()
        {
            Description(m => "Failed message resolved by retry");

            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}