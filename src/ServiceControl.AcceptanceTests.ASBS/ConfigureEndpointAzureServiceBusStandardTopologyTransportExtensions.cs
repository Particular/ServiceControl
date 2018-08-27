using NServiceBus;

public static class ConfigureEndpointAzureServiceBusStandardTransportExtensions
{
    public static void ConfigureASBS(this EndpointConfiguration configuration, string connectionString)
    {
        var transportConfig = configuration.UseTransport<AzureServiceBusTransport>();

        transportConfig.ConnectionString(connectionString);
    }
}