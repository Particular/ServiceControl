namespace ServiceControl.Contracts.EndpointControl
{
    using System;
    using NServiceBus;
    using Operations;

    public class EndpointStarted : IEvent
    {
        public EndpointDetails EndpointDetails { get; set; }
        public DateTime StartedAt { get; set; }
    }
}