namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageFailures;
    using ServiceControl.EventLog;

    class FailedMessageArchivedDefinition : EventLogMappingDefinition<FailedMessageArchived>
    {
        public FailedMessageArchivedDefinition()
        {
            Description(m => "Failed message deleted.");

            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}