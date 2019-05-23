namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class FailedMessagesUnArchived : IDomainEvent
    {
        public int MessagesCount { get; set; }
    }
}