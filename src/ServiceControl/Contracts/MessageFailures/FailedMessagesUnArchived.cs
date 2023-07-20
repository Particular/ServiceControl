namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class FailedMessagesUnArchived : IDomainEvent
    {
        public int MessagesCount { get; set; }
        public string[] DocumentIds { get; set; } //REFACTOR: The name suggest that this leaks persister specific info into the property name and/or value
    }
}