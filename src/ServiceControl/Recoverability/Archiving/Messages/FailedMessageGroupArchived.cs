namespace ServiceControl.Recoverability
{
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class FailedMessageGroupArchived : IDomainEvent, IUserInterfaceEvent
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public int MessagesCount { get; set; }
    }
}