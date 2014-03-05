namespace ServiceControl.Heartbeats.Sample
{
    using NServiceBus;

    public class EndpointConfig:IConfigureThisEndpoint,AsA_Server
    {
        public EndpointConfig()
        {
            Configure.Serialization.Json();
        }
    }
}
