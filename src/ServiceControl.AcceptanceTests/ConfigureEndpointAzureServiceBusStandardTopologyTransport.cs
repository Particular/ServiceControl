using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using ServiceBus.Management.AcceptanceTests;

public class ConfigureEndpointAzureServiceBusStandardTransport : ITransportIntegration
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        configuration.UseSerialization<NewtonsoftSerializer>();

        // workaround because of name collision between ASB and ASBS
        configuration.ConfigureASBS(ConnectionString);

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    public string Name => "AzureServiceBus - .NET Standard";
    public string TypeName => $"{typeof(ServiceControl.Transports.ASBS.ASBSTransportCustomization).AssemblyQualifiedName}";
    public string ConnectionString { get; set; }
}