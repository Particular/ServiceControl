namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using NServiceBus;

    public class EnableEndpointMonitoring : ICommand
    {
        public Guid EndpointId { get; set; }
    }

    public class DisableEndpointMonitoring : ICommand
    {
        public Guid EndpointId { get; set; }
    }
}