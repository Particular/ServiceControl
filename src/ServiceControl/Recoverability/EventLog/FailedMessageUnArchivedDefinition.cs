namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageFailures;
    using ServiceControl.EventLog;

    class FailedMessageUnArchivedDefinition : EventLogMappingDefinition<FailedMessagesUnArchived>
    {
        public FailedMessageUnArchivedDefinition()
        {
            Description(m => $"{m.MessagesCount} failed message(s) restored");
        }
    }
}
