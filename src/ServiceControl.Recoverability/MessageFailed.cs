namespace ServiceControl.Contracts.MessageFailures
{
    using System.Collections.Generic;
    using Infrastructure.DomainEvents;
    using Infrastructure.SignalR;
    using NServiceBus;
    using Operations;

    public class MessageFailed : IDomainEvent, IMessage, IUserInterfaceEvent
    {
        public string EndpointId { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string FailedMessageId { get; set; }
        public bool RepeatedFailure { get; set; }
        public EndpointDetails SendingEndpoint { get; set; }
        public EndpointDetails ReceivingEndpoint { get; set; }
    }
}