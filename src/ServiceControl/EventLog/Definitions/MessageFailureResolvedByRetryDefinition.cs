namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    public class MessageFailureResolvedByRetryDefinition : EventLogMappingDefinition<MessageFailureResolvedByRetry>
    {
        public MessageFailureResolvedByRetryDefinition()
        {
            Description(FormatDescription);
            RelatesToMessage(m => m.FailedMessageId);
        }

        static string FormatDescription(MessageFailureResolvedByRetry msg)
        {
            return string.IsNullOrEmpty(msg.FailedMessageType) 
                ? "Failed message resolved by retry" 
                : string.Format("Failed message {0} resolved by retry", msg.FailedMessageType);
        }
    }
}