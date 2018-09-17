namespace ServiceControl.EventLog.Definitions
{
    using Recoverability;

    class FailedMessageGroupArchivedDefinition : EventLogMappingDefinition<FailedMessageGroupArchived>
    {
        public FailedMessageGroupArchivedDefinition()
        {
            Description(m => $"Archived {m.MessagesCount} messages from group: {m.GroupName}");
            RelatesToGroup(m => m.GroupId);
        }
    }
}