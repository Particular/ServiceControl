using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using TestConventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusEndpointTopologyTransport : ITransportIntegration
{
    public string Name => "AzureServiceBus - Endpoint Topology";

    public string TypeName => "ServiceControl.Transports.ASB.ASBEndpointTopologyTransportCustomization, ServiceControl.Transports.ASB";

    public string ConnectionString { get; set; }
    
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        configuration.UseSerialization<NewtonsoftSerializer>();

        var transportConfig = configuration.UseTransport<AzureServiceBusTransport>();

        transportConfig.ConnectionString(ConnectionString);

        var endpointOrientedTopology = transportConfig.UseEndpointOrientedTopology();
        foreach (var publisher in publisherMetadata.Publishers)
        {
            foreach (var eventType in publisher.Events)
            {
                endpointOrientedTopology.RegisterPublisher(eventType, publisher.PublisherName);
            }
        }

        transportConfig.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}