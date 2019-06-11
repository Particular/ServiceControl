namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class MessageFailureResolvedByRetry : IEvent
    {
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}