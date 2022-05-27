namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageFailures;
    using ServiceControl.EventLog;

    class MessageFailureResolvedManuallyDefinition : EventLogMappingDefinition<MessageFailureResolvedManually>
    {
        public MessageFailureResolvedManuallyDefinition()
        {
            Description(_ => "Failed message resolved manually by user");
            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}