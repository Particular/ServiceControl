namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Infrastructure.SignalR;

    public class MessageFailed : IDomainEvent, IEvent, IUserInterfaceEvent
    {
        public string EndpointId{ get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string FailedMessageId { get; set; }
        public bool RepeatedFailure { get; set; }
    }
}
