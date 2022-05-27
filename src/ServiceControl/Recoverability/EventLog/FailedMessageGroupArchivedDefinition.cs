namespace ServiceControl.Recoverability.EventLog
{
    using Recoverability;
    using ServiceControl.EventLog;

    class FailedMessageGroupArchivedDefinition : EventLogMappingDefinition<FailedMessageGroupArchived>
    {
        public FailedMessageGroupArchivedDefinition()
        {
            Description(m => $"Deleted {m.MessagesCount} message(s) from group: {m.GroupName}");
            RelatesToGroup(m => m.GroupId);
        }
    }
}