namespace ServiceControl.Contracts.MessageFailures
{
    using ServiceControl.Infrastructure.DomainEvents;

    public class FailedMessageArchived : IDomainEvent
    {
        public string FailedMessageId { get; set; }
    }
}