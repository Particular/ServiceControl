namespace ServiceControl.Contracts.EndpointControl
{
    using NServiceBus;

    public class NewMachineDetectedForEndpoint:IEvent
    {
        public string Endpoint { get; set; }
        public string Machine { get; set; }
    }
}