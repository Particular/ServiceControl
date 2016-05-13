namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Contracts.MessageFailures;

    public class FailedMessageUnArchivedDefinition : EventLogMappingDefinition<FailedMessagesUnArchived>
    {
        public FailedMessageUnArchivedDefinition()
        {
            Description(m => $"{m.MessagesCount} failed message(s) unarchived");
        }
    }
}