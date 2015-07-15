namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class FailedMessageGroupArchived : IEvent
    {
        public string GroupId { get; set; }
    }
}