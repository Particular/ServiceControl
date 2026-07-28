namespace ServiceControl.Contracts.MessageFailures
{
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    public class MessageFailed : IDomainEvent, IMessage
    {
        public string EndpointId { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string FailedMessageId { get; set; }
        public bool RepeatedFailure { get; set; }
    }
}