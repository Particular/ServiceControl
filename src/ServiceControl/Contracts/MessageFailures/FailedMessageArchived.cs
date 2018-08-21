namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class FailedMessageArchived : IDomainEvent
    {
        public string FailedMessageId { get; set; }
    }
}