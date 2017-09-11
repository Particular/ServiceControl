namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class MessageFailureResolvedManually : IEvent
    {
        public string FailedMessageId { get; set; }
    }
}