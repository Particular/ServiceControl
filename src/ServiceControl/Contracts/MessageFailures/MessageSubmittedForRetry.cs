namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class MessageSubmittedForRetry : IDomainEvent, IUserInterfaceEvent
    {
        public string FailedMessageId { get; set; }
    }
}