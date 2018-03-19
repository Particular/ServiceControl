namespace ServiceControl.Contracts.MessageFailures
{
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class MessageSubmittedForRetry : IDomainEvent, IUserInterfaceEvent
    {
        public string FailedMessageId { get; set; }
    }
}