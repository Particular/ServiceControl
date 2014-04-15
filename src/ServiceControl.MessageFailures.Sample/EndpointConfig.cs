namespace ServiceControl.MessageFailures.Sample
{
    using NServiceBus;
    using NServiceBus.Features;

    public class EndpointConfig : IConfigureThisEndpoint, AsA_Server
    {
        public EndpointConfig()
        {
            Configure.Serialization.Json();
            Configure.Features.Disable<SecondLevelRetries>();
        }
    }
}
