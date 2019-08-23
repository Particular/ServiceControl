using System.Threading.Tasks;
using NServiceBus;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusForwardingTopologyTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration)
    {
        configuration.UseSerialization<NewtonsoftSerializer>();

        var transportConfig = configuration.UseTransport<AzureServiceBusTransport>();
        transportConfig.UseForwardingTopology();

        transportConfig.ConnectionString(ConnectionString);

        transportConfig.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    public string MonitoringSeamTypeName => typeof(ServiceControl.Transports.LegacyAzureServiceBus.ForwardingTopologyAzureServiceBusTransport).AssemblyQualifiedName;

    public string ConnectionString { get; set; }
}