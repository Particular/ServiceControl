namespace ServiceControl.Groups.Retry
{
    using ServiceControl.EventLog;

    public class RetryGroupEventLogDefinition : EventLogMappingDefinition<FailedMessageGroupRetried>
    {
        public RetryGroupEventLogDefinition()
        {
            Description(m => "Retried Failure Group");
            RelatesToGroup(m => m.GroupId);
        }
    }
}