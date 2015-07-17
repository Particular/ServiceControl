namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class FailedMessageGroupArchivedDefinition : EventLogMappingDefinition<FailedMessageGroupArchived>
    {
        public FailedMessageGroupArchivedDefinition()
        {
            Description(_ => "Failed Message Group Archived");
            RelatesToGroup(m => m.GroupId);
        }
    }
}
