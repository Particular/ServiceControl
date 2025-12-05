namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class MessageEditedAndRetried : IDomainEvent
    {
        public string FailedMessageId { get; set; }
    }
}