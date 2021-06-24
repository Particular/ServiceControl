namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;
    using NServiceBus;

    // Comes from unconverted legacy instances
    public class MessageFailureResolvedByRetry : IDomainEvent, IMessage, IUserInterfaceEvent
    {
        public bool StateUpdated { get; set; }
        public string FailedMessageId { get; set; }
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}