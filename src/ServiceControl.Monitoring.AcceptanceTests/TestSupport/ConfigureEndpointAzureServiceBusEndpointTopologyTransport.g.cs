using System.Threading.Tasks;
using NServiceBus;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusEndpointTopologyTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration)
    {
        configuration.UseSerialization<NewtonsoftSerializer>();

        var transportConfig = configuration.UseTransport<NServiceBus.AzureServiceBusTransport>();

        transportConfig.ConnectionString(ConnectionString);

        var endpointOrientedTopology = transportConfig.UseEndpointOrientedTopology();

        transportConfig.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    public string MonitoringSeamTypeName => typeof(ServiceControl.Transports.LegacyAzureServiceBus.EndpointOrientedTopologyAzureServiceBusTransport).AssemblyQualifiedName;
    public string ConnectionString { get; set; }
}