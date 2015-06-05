namespace ServiceControl.Groups.Archive
{
    using NServiceBus;

    public class FailedMessageGroupArchived : IEvent
    {
        public string GroupId { get; set; }
    }
}