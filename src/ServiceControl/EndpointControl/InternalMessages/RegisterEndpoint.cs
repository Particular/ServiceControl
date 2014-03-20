namespace ServiceControl.EndpointControl.InternalMessages
{
    using System;
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    public class RegisterEndpoint : ICommand
    {
        public Guid EndpointInstanceId { get; set; }
        public EndpointDetails Endpoint { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}