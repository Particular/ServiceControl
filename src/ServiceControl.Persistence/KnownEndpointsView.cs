namespace ServiceControl.Persistence
{
    using System;
    using ServiceControl.Operations;

    public class KnownEndpointsView
    {
        public Guid Id { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public string HostDisplayName { get; set; }
    }
}