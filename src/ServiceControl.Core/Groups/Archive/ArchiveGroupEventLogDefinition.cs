namespace ServiceControl.Groups.Archive
{
    using ServiceControl.EventLog;

    public class ArchiveGroupEventLogDefinition : EventLogMappingDefinition<FailedMessageGroupArchived>
    {
        public ArchiveGroupEventLogDefinition()
        {
            Description(m => "Archived Failure Group");
            RelatesToGroup(m => m.GroupId);
        }
    }
}