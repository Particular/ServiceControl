namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using Contracts.Operations;

    public class KnownEndpointsView
    {
        public Guid Id { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public string HostDisplayName { get; set; }
    }
}