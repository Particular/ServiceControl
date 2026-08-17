namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;

    public class FailedMessageGroupArchived : IDomainEvent
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public int MessagesCount { get; set; }
    }

    public class FailedMessageGroupBatchArchived : IDomainEvent
    {
        public string[] FailedMessagesIds { get; set; }
    }
}