namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class MessageFailureResolvedByRetry : IDomainEvent, IBusEvent
    {
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}