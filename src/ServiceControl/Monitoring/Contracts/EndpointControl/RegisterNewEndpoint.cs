namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using NServiceBus;
    using Operations;

    public class RegisterNewEndpoint : ICommand
    {
        public DateTime DetectedAt { get; set; }
        public EndpointDetails Endpoint { get; set; }
    }
}