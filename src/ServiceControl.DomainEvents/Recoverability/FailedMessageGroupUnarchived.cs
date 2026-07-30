namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;

    public class FailedMessageGroupUnarchived : IDomainEvent
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public int MessagesCount { get; set; }
    }
}