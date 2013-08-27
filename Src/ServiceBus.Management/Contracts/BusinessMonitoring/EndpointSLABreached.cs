namespace ServiceControl.Contracts.BusinessMonitoring
{
    using NServiceBus;

    public class EndpointSLABreached : IEvent
    {
        public string Endpoint { get; set; }
    }
}