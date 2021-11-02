namespace NServiceBus
{
    public static class ServicePlatformConnectionExtensions
    {
        public static void ConnectToServicePlatform(this EndpointConfiguration endpointConfiguration, ServicePlatformConnectionConfiguration servicePlatformConnectionConfiguration)
        {
            Guard.AgainstNull(nameof(endpointConfiguration), endpointConfiguration);
            Guard.AgainstNull(nameof(servicePlatformConnectionConfiguration), servicePlatformConnectionConfiguration);

            servicePlatformConnectionConfiguration.ApplyTo(endpointConfiguration);
        }
    }
}