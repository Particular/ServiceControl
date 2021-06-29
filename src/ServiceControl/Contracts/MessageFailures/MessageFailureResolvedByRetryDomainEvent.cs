namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;

    public class MessageFailureResolvedByRetryDomainEvent : IDomainEvent, IUserInterfaceEvent
    {
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}