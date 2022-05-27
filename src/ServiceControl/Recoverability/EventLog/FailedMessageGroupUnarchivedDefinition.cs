namespace ServiceControl.Recoverability.EventLog
{
    using Recoverability;
    using ServiceControl.EventLog;

    class FailedMessageGroupUnarchivedDefinition : EventLogMappingDefinition<FailedMessageGroupUnarchived>
    {
        public FailedMessageGroupUnarchivedDefinition()
        {
            Description(m => $"Restored {m.MessagesCount} message(s) from group: {m.GroupName}");
            RelatesToGroup(m => m.GroupId);
        }
    }
}