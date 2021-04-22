namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class FailedMessageGroupArchived : IDomainEvent, IUserInterfaceEvent
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public int MessagesCount { get; set; }
    }

    public class FailedMessageGroupBatchArchived : IDomainEvent, IUserInterfaceEvent
    {
        public string[] FailedMessagesIds { get; set; }
    }
}