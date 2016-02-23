namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class FailedMessageGroupArchivedDefinition : EventLogMappingDefinition<FailedMessageGroupArchived>
    {
        public FailedMessageGroupArchivedDefinition()
        {
            Description(m => string.Format("Archived {0} messages from group: {1}", m.MessagesCount, m.GroupName));
            RelatesToGroup(m => m.GroupId);
        }
    }
}
