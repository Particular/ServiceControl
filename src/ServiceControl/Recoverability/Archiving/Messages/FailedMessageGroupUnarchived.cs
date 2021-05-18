// unset

namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class FailedMessageGroupUnarchived : IDomainEvent, IUserInterfaceEvent
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public int MessagesCount { get; set; }
    }
}