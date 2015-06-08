namespace ServiceControl.Groups.Retry
{
    using NServiceBus;

    public class FailedMessageGroupRetried : IEvent
    {
        public string GroupId { get; set; }
    }
}