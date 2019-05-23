using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceBus.Management.AcceptanceTests;
using ServiceControlInstaller.Engine.Instances;

public class ConfigureEndpointAzureServiceBusForwardingTopologyTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
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

    public string Name => TransportNames.AzureServiceBusForwardingTopology;

    public string TypeName => $"{typeof(ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization).AssemblyQualifiedName}";

    public string ConnectionString { get; set; }
}