namespace ServiceControl.EndpointControl.InternalMessages
{
    using System;
    using NServiceBus;
    using ServiceControl.Operations;

    class RegisterEndpoint : ICommand
    {
        public Guid EndpointInstanceId { get; set; }
        public EndpointDetails Endpoint { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool EnableMonitoring { get; set; }
    }
}