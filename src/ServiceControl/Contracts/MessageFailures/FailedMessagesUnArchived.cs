namespace ServiceControl.Contracts.MessageFailures
{
    using ServiceControl.Infrastructure.DomainEvents;

    public class FailedMessagesUnArchived: IDomainEvent
    {
        public int MessagesCount { get; set; }
    }
}