namespace ServiceControl.Groups.Retry
{
    using ServiceControl.EventLog;

    public class RetryGroupEventLogDefinition : EventLogMappingDefinition<BulkRetryCompleted>
    {
        public RetryGroupEventLogDefinition()
        {
            Description(m => m.RanToCompletion
                ? "Bulk Retry Completed"
                : "Bulk Retry Stopped");
            RelatesToGroup(m => m.GroupId);
        }
    }
}