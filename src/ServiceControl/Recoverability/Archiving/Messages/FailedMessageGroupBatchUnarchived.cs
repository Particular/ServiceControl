namespace ServiceControl.Recoverability
{
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class FailedMessageGroupBatchUnarchived : IDomainEvent, IUserInterfaceEvent
    {
        public string[] FailedMessagesIds { get; set; }
    }
}