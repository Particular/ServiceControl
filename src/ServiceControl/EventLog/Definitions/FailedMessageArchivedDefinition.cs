namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    class FailedMessageArchivedDefinition : EventLogMappingDefinition<FailedMessageArchived>
    {
        public FailedMessageArchivedDefinition()
        {
            Description(m => "Failed message archived.");

            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}