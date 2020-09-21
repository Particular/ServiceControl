namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;

    public class FailedMessagesUnArchived : IDomainEvent
    {
        public long MessagesCount { get; set; }
    }
}