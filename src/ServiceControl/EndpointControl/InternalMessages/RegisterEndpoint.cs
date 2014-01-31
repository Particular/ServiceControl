namespace ServiceControl.EndpointControl.InternalMessages
{
    using System;
    using Contracts.Operations;
    using NServiceBus;

    public class RegisterEndpoint : ICommand
    {
        public EndpointDetails Endpoint { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}