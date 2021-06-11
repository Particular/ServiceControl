namespace ServiceControl.EventLog.Definitions
{
    using Recoverability;

    class FailedMessageGroupUnarchivedDefinition : EventLogMappingDefinition<FailedMessageGroupUnarchived>
    {
        public FailedMessageGroupUnarchivedDefinition()
        {
            Description(m => $"Restored {m.MessagesCount} message(s) from group: {m.GroupName}");
            RelatesToGroup(m => m.GroupId);
        }
    }
}