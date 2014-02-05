namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using NServiceBus;

    public class KnownEndpointUpdate : ICommand
    {
        public Guid KnownEndpointId { get; set; }
        public bool MonitorHeartbeat { get; set; }
    }
}