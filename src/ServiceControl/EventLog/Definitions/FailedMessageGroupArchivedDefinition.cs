namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class FailedMessageGroupArchivedDefinition : EventLogMappingDefinition<FailedMessageGroupArchived>
    {
        public FailedMessageGroupArchivedDefinition()
        {
            Description(m => string.Format("Archive {0} messages from group: {1}", m.MessageIds.Length, m.GroupName));
            RelatesToGroup(m => m.GroupId);
            RelatesToMessages(m => m.MessageIds);
        }
    }
}
