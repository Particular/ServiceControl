namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class FailedMessageArchived : IEvent
    {
        public string FailedMessageId { get; set; }
    }
}