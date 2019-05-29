namespace ServiceControl.Audit.Monitoring
{
    using System;

    public class KnownEndpointsView
    {
        public Guid Id { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public string HostDisplayName { get; set; }
    }
}