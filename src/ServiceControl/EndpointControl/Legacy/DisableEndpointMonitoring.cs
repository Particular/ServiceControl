namespace ServiceControl.CompositeViews.Endpoints
{
    using System;
    using NServiceBus;

    public class DisableEndpointMonitoring : ICommand
    {
        public Guid EndpointId { get; set; }
    }
}