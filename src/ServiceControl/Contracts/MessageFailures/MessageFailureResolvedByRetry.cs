namespace ServiceControl.Contracts.MessageFailures
{
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class MessageFailureResolvedByRetry : IDomainEvent, IBusEvent, IUserInterfaceEvent
    {
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}