namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class MessageFailureResolved : IEvent
    {
        public string FailedMessageId { get; set; }
    }
}