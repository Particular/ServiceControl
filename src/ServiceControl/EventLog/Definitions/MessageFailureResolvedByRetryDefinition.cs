namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    class MessageFailureResolvedByRetryDefinition : EventLogMappingDefinition<MessageFailureResolvedByRetryDomainEvent>
    {
        public MessageFailureResolvedByRetryDefinition()
        {
            Description(_ => "Failed message resolved by retry");
            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}