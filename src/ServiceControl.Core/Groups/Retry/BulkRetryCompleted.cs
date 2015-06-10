namespace ServiceControl.Groups.Retry
{
    using NServiceBus;

    public class BulkRetryCompleted : IEvent
    {
        public string GroupId { get; set; }
        public bool RanToCompletion { get; set; }
    }
}