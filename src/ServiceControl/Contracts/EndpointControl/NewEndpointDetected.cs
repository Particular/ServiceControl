namespace ServiceControl.Contracts.EndpointControl
{
    using NServiceBus;

    public class NewEndpointDetected:IEvent
    {
        public string Endpoint { get; set; }
    }
}