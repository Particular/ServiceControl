using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using TestConventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusForwardingTopologyTransport : ITransportIntegration
{
    public string Name => "AzureServiceBus - Forwarding Topology";

    public string TypeName => "ServiceControl.Transports.ASB.ASBForwardingTopologyTransportCustomization, ServiceControl.Transports.ASB";

    public string ConnectionString { get; set; }
    
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
}