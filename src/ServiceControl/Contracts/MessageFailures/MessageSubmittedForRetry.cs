namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class MessageSubmittedForRetry : IEvent
    {
        public string FailedMessageId { get; set; }
    }
}