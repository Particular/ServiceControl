namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using Audit.Monitoring;
    using NServiceBus;

    public class RegisterNewEndpoint : ICommand
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}