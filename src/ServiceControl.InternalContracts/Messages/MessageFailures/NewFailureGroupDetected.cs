namespace ServiceControl.InternalContracts.Messages.MessageFailures
{
    using NServiceBus;

    public class NewFailureGroupDetected : IEvent
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
    }
}
