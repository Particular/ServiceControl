namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class MessageSubmittedForRetry : IDomainEvent
    {
        public string FailedMessageId { get; set; }
    }
}