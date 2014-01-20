namespace ServiceControl.IntegrationDemo
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
