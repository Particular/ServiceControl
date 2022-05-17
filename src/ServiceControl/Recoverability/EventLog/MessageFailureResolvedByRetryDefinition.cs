namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageFailures;
    using ServiceControl.EventLog;

    class MessageFailureResolvedByRetryDefinition : EventLogMappingDefinition<MessageFailureResolvedByRetry>
    {
        public MessageFailureResolvedByRetryDefinition()
        {
            Description(_ => "Failed message resolved by retry");
            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}