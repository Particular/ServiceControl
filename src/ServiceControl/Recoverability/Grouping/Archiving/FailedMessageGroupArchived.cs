namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class FailedMessageGroupArchived : IEvent
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string[] MessageIds { get; set; }
    }
}