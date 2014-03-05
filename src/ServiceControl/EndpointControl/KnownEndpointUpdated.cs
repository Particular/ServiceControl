namespace ServiceControl.EndpointControl
{
    using System;
    using NServiceBus;

    public class KnownEndpointUpdated : IEvent
    {
        public Guid KnownEndpointId { get; set; }
        public string Name { get; set; }
        public string HostDisplayName { get; set; }
        public Guid HostId { get; set; }
    }
}