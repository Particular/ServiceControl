namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    public class FailedMessageArchivedDefinition : EventLogMappingDefinition<FailedMessageArchived>
    {
        public FailedMessageArchivedDefinition()
        {
            Description(m => "Failed message archived.");

            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}